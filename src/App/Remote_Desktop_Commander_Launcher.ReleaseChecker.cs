using System;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

// Reads our small public version file, avoiding GitHub REST API quotas shared
// across apps on the same internet connection. Never downloads/installs software.
internal sealed class ReleaseChecker : IDisposable
{
    private const string PreferencesKey = @"Software\ZeroFlagLabs\CommanderCompanion";
    private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
    private bool disposed;
    internal event EventHandler Changed;
    internal bool Checking { get; private set; }
    internal ReleaseInfo Result { get; private set; }

    internal ReleaseChecker() { Result = ReleaseInfo.Message("Not checked yet."); }

    internal bool CheckOnLaunch
    {
        get {
            using (var key = Registry.CurrentUser.OpenSubKey(PreferencesKey))
                return key == null || !object.Equals(key.GetValue("CheckForUpdates", 1), 0);
        }
        set {
            using (var key = Registry.CurrentUser.CreateSubKey(PreferencesKey))
                key.SetValue("CheckForUpdates", value ? 1 : 0, RegistryValueKind.DWord);
            Notify();
        }
    }

    internal async Task CheckAtLaunchAsync()
    {
        // A preference read failure must not prevent the connection controls opening.
        try { if (!CheckOnLaunch) return; }
        catch (System.Security.SecurityException) { return; }
        catch (UnauthorizedAccessException) { return; }
        await CheckAsync();
    }

    internal async Task CheckAsync()
    {
        if (Checking || disposed) return;
        Checking = true;
        Result = ReleaseInfo.Message("Checking GitHub Releases…");
        Notify();
        var token = lifetime.Token;
        try { Result = await Task.Run(() => Fetch(token)); }
        catch (OperationCanceledException) { }
        catch (Exception) { Result = Unavailable(); }
        finally {
            Checking = false;
            if (!disposed) Notify();
            else lifetime.Dispose();
        }
    }

    private static ReleaseInfo Fetch(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var request = (HttpWebRequest)WebRequest.Create(AppInfo.VersionManifestUrl);
        request.UserAgent = "ZeroFlagLabs-CommanderCompanion/" + AppInfo.VersionText;
        request.Accept = "application/json";
        request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
        request.AllowAutoRedirect = false;
        request.Timeout = 7000;
        request.ReadWriteTimeout = 7000;
        using (token.Register(request.Abort))
        try {
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var data = new MemoryStream()) {
                if (response.StatusCode != HttpStatusCode.OK) return Unavailable();
                var buffer = new byte[8192];
                int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) != 0) {
                    token.ThrowIfCancellationRequested();
                    if (data.Length + count > 1048576) return Unavailable();
                    data.Write(buffer, 0, count);
                }
                return ReleaseInfo.Parse(Encoding.UTF8.GetString(data.ToArray()), AppInfo.Version);
            }
        }
        catch (WebException error) {
            using (var response = error.Response as HttpWebResponse) {
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                    return ReleaseInfo.Message("Update information is unavailable. You can still open the release page.");
                if (response != null && ((int)response.StatusCode == 403 || (int)response.StatusCode == 429))
                    return ReleaseInfo.Message("The version file is temporarily unavailable. Please try again later.");
            }
            return Unavailable();
        }
        catch (IOException) { return Unavailable(); }
        catch (ArgumentException) { return Unavailable(); }
        catch (InvalidOperationException) { return Unavailable(); }
        catch (FormatException) { return Unavailable(); }
    }

    private static ReleaseInfo Unavailable()
    {
        return ReleaseInfo.Message("Unable to check updates. Please try again when online.");
    }

    private void Notify() { if (Changed != null) Changed(this, EventArgs.Empty); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        if (!Checking) lifetime.Dispose();
    }
}
