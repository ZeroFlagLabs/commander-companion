using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

// The website owns sign-in and its cookies. This window never reads passwords,
// browser cookies, device tokens, or the website's general account response.
internal sealed class UsageAccountWindow : Form
{
    private readonly WebView2 browser = new WebView2();
    private readonly Label address = new Label();
    private readonly Timer visibleTimer = new Timer { Interval = 10000 };
    private readonly List<Form> popups = new List<Form>();
    private readonly TaskCompletionSource<bool> firstNavigation = new TaskCompletionSource<bool>();
    private CoreWebView2Environment environment;
    private Task initialise;
    private Task<UsageSnapshot> refresh;
    private TaskCompletionSource<UsageSnapshot> pending;
    private string requestId;
    private bool closing;
    private bool navigationHealthy;
    private bool closeAfterSignIn;
    private readonly string profileOverride;
    internal string Diagnostic = "Not started";
    internal event Action<UsageSnapshot> Updated;

    internal UsageAccountWindow(string accountProfile = null)
    {
        profileOverride = accountProfile;
        Text = "Desktop Commander account";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Font = new Font("Segoe UI", 9F);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1000, 710);
        MinimumSize = new Size(640, 480);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        address.Dock = DockStyle.Fill;
        address.Height = 35;
        address.Padding = new Padding(12, 0, 0, 0);
        address.TextAlign = ContentAlignment.MiddleLeft;
        address.Text = "Official account website: mcp.desktopcommander.app";
        var external = new LinkLabel { Text = "Open in browser", AutoSize = true, Dock = DockStyle.Right, Padding = new Padding(8) };
        external.LinkClicked += delegate { OpenWebsite(); };
        var bar = new Panel { Dock = DockStyle.Top, Height = 35, BackColor = Color.FromArgb(242, 244, 247) };
        bar.Controls.Add(address); bar.Controls.Add(external);
        browser.Dock = DockStyle.Fill;
        Controls.Add(browser); Controls.Add(bar);
        FormClosing += delegate(object sender, FormClosingEventArgs e) {
            if (!closing && e.CloseReason == CloseReason.UserClosing) {
                e.Cancel = true; Hide(); ShowInTaskbar = false;
                BeginInvoke(new Action(async delegate { await RefreshUsageAsync(); }));
            }
        };
        visibleTimer.Tick += async delegate {
            if (Visible) await RefreshUsageAsync();
        };
        visibleTimer.Start();
    }

    internal Task<UsageSnapshot> RefreshUsageAsync()
    {
        if (closing) return Task.FromResult(UsageSnapshot.Status(UsageState.Unavailable));
        if (refresh == null || refresh.IsCompleted) refresh = FetchAsync();
        return refresh;
    }

    private Task EnsureAsync()
    {
        if (initialise == null) initialise = InitialiseAsync();
        return initialise;
    }

    private async Task InitialiseAsync()
    {
        Diagnostic = "Initialising account browser";
        // Browser data stays in this Windows user's app-data, outside the source
        // project and OneDrive. It is independent of Chrome/Edge's normal profile.
        string profile = profileOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZeroFlagLabs", "RemoteDesktopCommanderLauncher", "AccountBrowser");
        if (Handle == IntPtr.Zero || browser.Handle == IntPtr.Zero) throw new InvalidOperationException("Account window could not be created.");
        environment = await CoreWebView2Environment.CreateAsync(null, profile);
        Diagnostic = "Browser environment ready";
        if (closing) return;
        await browser.EnsureCoreWebView2Async(environment);
        Diagnostic = "Browser control ready";
        if (closing) return;
        Configure(browser.CoreWebView2);
        browser.CoreWebView2.NavigationStarting += NavigationStarting;
        browser.CoreWebView2.SourceChanged += delegate {
            Uri uri;
            if (Uri.TryCreate(browser.CoreWebView2.Source, UriKind.Absolute, out uri))
                address.Text = "Account website: " + uri.Host;
        };
        browser.CoreWebView2.NavigationCompleted += async delegate(object sender, CoreWebView2NavigationCompletedEventArgs e) {
            navigationHealthy = e.IsSuccess;
            Uri current;
            string host = Uri.TryCreate(browser.CoreWebView2.Source, UriKind.Absolute, out current) ? current.Host : "unknown";
            Diagnostic = "Navigation " + (e.IsSuccess ? "ready" : e.WebErrorStatus.ToString()) + " (" + host + ")";
            firstNavigation.TrySetResult(e.IsSuccess);
            if (closing) return;
            // After a user signs in, the vendor redirects back to the dashboard.
            // Read fresh usage then; do not copy any login token out of the page.
            if (e.IsSuccess && UsageOrigin.IsDashboard(browser.CoreWebView2.Source)) await RefreshUsageAsync();
            else if (e.IsSuccess && host == "auth.desktopcommander.app") {
                var signedOut = UsageSnapshot.Status(UsageState.SignedOut);
                if (pending != null) pending.TrySetResult(signedOut);
                Publish(signedOut);
            }
        };
        browser.CoreWebView2.WebMessageReceived += MessageReceived;
        browser.CoreWebView2.NewWindowRequested += NewWindow;
        browser.CoreWebView2.Navigate(UsageOrigin.Page);
    }

    private static void Configure(CoreWebView2 core)
    {
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
    }

    private static void NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
    {
        // HTTPS identity-provider redirects are allowed, but local files and
        // executable/custom protocols are never opened by the embedded page.
        if (!UsageOrigin.IsSecurePage(e.Uri) && e.Uri != "about:blank") e.Cancel = true;
    }

    private async void NewWindow(object sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (closing || !Visible || (!UsageOrigin.IsSecurePage(e.Uri) && e.Uri != "about:blank")) return;
        var deferral = e.GetDeferral();
        Form popup = null;
        try
        {
            popup = new Form { Text = "Desktop Commander sign-in", Icon = Icon,
                ClientSize = new Size(650, 700), StartPosition = FormStartPosition.CenterParent };
            var view = new WebView2 { Dock = DockStyle.Fill };
            var origin = new Label { Dock = DockStyle.Top, Height = 30, Padding = new Padding(10, 6, 0, 0), Text = "Opening secure sign-in…" };
            popup.Controls.Add(view); popup.Controls.Add(origin);
            popups.Add(popup);
            popup.FormClosed += delegate { popups.Remove(popup); };
            popup.Show(this);
            await view.EnsureCoreWebView2Async(environment);
            Configure(view.CoreWebView2);
            view.CoreWebView2.NavigationStarting += NavigationStarting;
            view.CoreWebView2.SourceChanged += delegate {
                Uri uri;
                if (Uri.TryCreate(view.CoreWebView2.Source, UriKind.Absolute, out uri)) origin.Text = "Sign-in website: " + uri.Host;
            };
            view.CoreWebView2.WindowCloseRequested += delegate { popup.Close(); };
            e.NewWindow = view.CoreWebView2;
        }
        catch { if (popup != null) popup.Close(); }
        finally { deferral.Complete(); }
    }

    private async Task<UsageSnapshot> FetchAsync()
    {
        UsageSnapshot result;
        try
        {
            await EnsureAsync();
            if (closing) return UsageSnapshot.Status(UsageState.Unavailable);
            if (await Task.WhenAny(firstNavigation.Task, Task.Delay(15000)) != firstNavigation.Task)
            {
                Diagnostic = "Navigation timed out";
                return Publish(UsageSnapshot.Status(UsageState.Unavailable));
            }
            if (!navigationHealthy) {
                browser.CoreWebView2.Reload();
                return Publish(UsageSnapshot.Status(UsageState.Unavailable));
            }
            if (!UsageOrigin.IsDashboard(browser.CoreWebView2.Source))
                return Publish(UsageSnapshot.Status(UsageState.SignedOut));
            requestId = Guid.NewGuid().ToString("N");
            Diagnostic = "Requesting account usage";
            pending = new TaskCompletionSource<UsageSnapshot>();
            // Run the same read-only request the vendor dashboard uses, inside
            // its signed-in browser context. Only the usage object leaves it.
            string script = @"(async function () {
                const id = '" + requestId + @"';
                const send = data => window.chrome.webview.postMessage(Object.assign({kind:'zfl-usage',requestId:id},data));
                const abort = new AbortController();
                const timeout = setTimeout(() => abort.abort(), 10000);
                try {
                    const sessionResponse = await fetch('/auth/session', {
                        credentials:'include',cache:'no-store',redirect:'error',signal:abort.signal
                    });
                    if (!sessionResponse.ok) { send({state:'unavailable'}); return; }
                    const session = await sessionResponse.json();
                    if (session.authenticated !== true) { send({state:'signedOut'}); return; }
                    const response = await fetch('" + UsageOrigin.Endpoint + @"', {
                        credentials:'include',cache:'no-store',redirect:'error',signal:abort.signal
                    });
                    if (response.status === 401) { send({state:'signedOut'}); return; }
                    if (!response.ok) { send({state:'unavailable'}); return; }
                    const data = await response.json();
                    send({state:'ready',usage:data.usage});
                } catch (_) { send({state:'unavailable'}); }
                finally { clearTimeout(timeout); }
            })();";
            Task execute = browser.CoreWebView2.ExecuteScriptAsync(script);
            if (await Task.WhenAny(execute, Task.Delay(15000)) != execute)
            {
                Diagnostic = "Script timed out";
                return Publish(UsageSnapshot.Status(UsageState.Unavailable));
            }
            await execute;
            result = await Task.WhenAny(pending.Task, Task.Delay(15000)) == pending.Task
                ? await pending.Task : UsageSnapshot.Status(UsageState.Unavailable);
        }
        catch (Exception ex) {
            Diagnostic += "; " + ex.GetType().Name + " (" + ex.HResult.ToString("X8") + ")";
            result = UsageSnapshot.Status(UsageState.Unavailable);
        }
        finally { requestId = null; pending = null; }
        return Publish(result);
    }

    private void MessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (closing || pending == null || !UsageOrigin.IsDashboard(e.Source)) return;
        try
        {
            string json = e.WebMessageAsJson;
            if (json.Length > 32768) return;
            var serializer = new JavaScriptSerializer { MaxJsonLength = 32768, RecursionLimit = 12 };
            var message = serializer.DeserializeObject(json) as Dictionary<string, object>;
            object kind, id, state;
            if (message == null || !message.TryGetValue("kind", out kind) || !Equals(kind, "zfl-usage")
                || !message.TryGetValue("requestId", out id) || !UsageOrigin.IsExpectedMessage(e.Source, requestId, id as string)
                || !message.TryGetValue("state", out state)) return;
            Diagnostic = "Account response: " + Convert.ToString(state);
            pending.TrySetResult(Equals(state, "ready") ? UsageParser.Parse(json, DateTimeOffset.UtcNow)
                : UsageSnapshot.Status(Equals(state, "signedOut") ? UsageState.SignedOut : UsageState.Unavailable));
        }
        catch { pending.TrySetResult(UsageSnapshot.Status(UsageState.Unavailable)); }
    }

    private UsageSnapshot Publish(UsageSnapshot snapshot)
    {
        if (!closing && closeAfterSignIn && (snapshot.State == UsageState.Free || snapshot.State == UsageState.Pro)) {
            closeAfterSignIn = false;
            Hide(); ShowInTaskbar = false;
        }
        if (!closing && Updated != null) Updated(snapshot);
        return snapshot;
    }

    internal async Task OpenAccountAsync(Form owner)
    {
        try
        {
            await EnsureAsync();
            if (closing) return;
            closeAfterSignIn = true;
            ShowInTaskbar = true;
            if (!Visible) Show(owner);
            Activate();
        }
        catch { OpenWebsite(); }
    }

    internal static void OpenWebsite()
    {
        try { Process.Start(new ProcessStartInfo(UsageOrigin.Page) { UseShellExecute = true }); }
        catch { /* Keep the connection controls usable if no browser is registered. */ }
    }

    protected override void Dispose(bool disposing)
    {
        closing = true;
        if (disposing)
        {
            visibleTimer.Stop(); visibleTimer.Dispose();
            firstNavigation.TrySetResult(false);
            if (pending != null) pending.TrySetResult(UsageSnapshot.Status(UsageState.Unavailable));
            foreach (var popup in new List<Form>(popups)) popup.Dispose();
        }
        base.Dispose(disposing);
    }
}
