using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class CommanderWindow : Form
{
    private readonly bool background;
    private readonly ReleaseChecker updates = new ReleaseChecker();
    private readonly ToolTip tips = new ToolTip();
    private readonly UsageCard usage = new UsageCard();
    private readonly System.Windows.Forms.Timer usageTimer = new System.Windows.Forms.Timer();
    private UsageAccountWindow usageAccount;
    private DateTime lastUsageAttempt;
    private bool refreshingUsage;
    private readonly StartupOption startup = new StartupOption();
    private readonly StatusView status = new StatusView();
    private readonly Label detail = new Label(), foot = new Label();
    private readonly Button connect = new Button();
    private readonly Button terminal = new Button();
    private readonly CheckBox startWithWindows = new CheckBox();
    private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    private bool busy, loading, running, connecting, failedStart;
    private Process launchProcess;
    private DateTime launchTime;
    protected override bool ShowWithoutActivation
    {
        get
        {
            return background;
        }
    }

    internal CommanderWindow(bool startupLaunch, bool startMinimised = false)
    {
        bool enabled = startup.Enabled;
        background = startupLaunch || startMinimised;
        Text = Program.Title;
        Font = new Font("Segoe UI", 10F);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(480, 414);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MinimizeBox = true;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(247, 248, 250);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (background)
            WindowState = FormWindowState.Minimized;
        var picture = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            Bounds = new Rectangle(27, 23, 43, 50)
        };
        string png = Path.Combine(Program.Resources, "Remote_Desktop_Commander_Launcher.png");
        if (File.Exists(png))
            using (var image = Image.FromFile(png))
                picture.Image = new Bitmap(image);
        else
            picture.Image = Icon.ToBitmap();
        var heading = new Label
        {
            Text = Program.Title,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            Bounds = new Rectangle(83, 22, 333, 29)
        };
        var subtitle = new Label
        {
            Text = "Connect this PC to ChatGPT via Desktop Commander Remote",
            ForeColor = Color.DimGray,
            Bounds = new Rectangle(84, 53, 360, 23),
            Font = new Font("Segoe UI", 9F)
        };
        var about = new AboutButton { Bounds = new Rectangle(428, 23, 26, 26), BackColor = BackColor };
        tips.SetToolTip(about, "About and updates");
        about.Click += delegate { using (var dialog = new AboutWindow(updates)) dialog.ShowDialog(this); };
        status.SetBounds(20, 99, 440, 39);
        detail.SetBounds(26, 146, 428, 42);
        detail.ForeColor = Color.FromArgb(65, 70, 78);
        detail.TextAlign = ContentAlignment.TopCenter;
        connect.SetBounds(26, 201, 134, 37);
        connect.Enabled = false;
        connect.Text = "Checking...";
        connect.Click += async delegate
        {
            await ChangeConnection(false);
        };
        terminal.Text = "Open terminal";
        terminal.SetBounds(173, 201, 134, 37);
        terminal.Enabled = false;
        terminal.Click += async delegate
        {
            await OpenTerminal();
        };
        var minimise = new Button
        {
            Text = "Minimise",
            Bounds = new Rectangle(320, 201, 134, 37)
        };
        minimise.Click += delegate
        {
            WindowState = FormWindowState.Minimized;
        };
        usage.SetBounds(26, 253, 428, 108);
        usage.RefreshRequested += async delegate { await RefreshUsage(true); };
        usage.OpenAccount += async delegate {
            if (usage.Snapshot.State == UsageState.SignedOut) {
                EnsureUsageAccount();
                await usageAccount.OpenAccountAsync(this);
            }
            else UsageAccountWindow.OpenWebsite();
        };
        foot.SetBounds(26, 376, 294, 25);
        foot.Font = new Font("Segoe UI", 9F);
        foot.ForeColor = Color.DimGray;
        foot.TextAlign = ContentAlignment.MiddleLeft;
        foot.AutoEllipsis = true;
        startWithWindows.Text = "Start with Windows";
        startWithWindows.Font = new Font("Segoe UI", 9F);
        startWithWindows.AutoSize = true;
        startWithWindows.Size = startWithWindows.PreferredSize;
        startWithWindows.Location = new Point(ClientSize.Width - 26 - startWithWindows.Width, 379);
        startWithWindows.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        loading = true;
        startWithWindows.Checked = enabled;
        loading = false;
        startWithWindows.CheckedChanged += delegate
        {
            if (loading)
                return;
            try
            {
                startup.Enabled = startWithWindows.Checked;
            }
            catch (Exception ex)
            {
                loading = true;
                startWithWindows.Checked = !startWithWindows.Checked;
                loading = false;
                MessageBox.Show(this, "The startup option could not be saved.\r\n" + ex.Message, Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        Controls.AddRange(new Control[] { picture, heading, subtitle, about, status, detail, connect, terminal, minimise, usage, foot, startWithWindows });
        SetState("checking", "Checking status...", "Checking for an existing Desktop Commander Remote session.");
        timer.Interval = 3000;
        timer.Tick += async delegate
        {
            await RefreshStatus();
        };
        Shown += async delegate
        {
            await RefreshStatus();
            timer.Start();
            var updateTask = updates.CheckAtLaunchAsync();
            await RefreshUsage(false);
            // Only an enabled sign-in launch connects automatically.
            if (startupLaunch && enabled && !running)
                await ChangeConnection(true);
            await updateTask;
        };
        usageTimer.Interval = 60000;
        usageTimer.Tick += async delegate { await RefreshUsage(false); };
        usageTimer.Start();
        Activated += async delegate { await RefreshUsage(false); };
        FormClosed += delegate
        {
            updates.Dispose();
            tips.Dispose();
            usageTimer.Stop(); usageTimer.Dispose();
            if (usageAccount != null) usageAccount.Dispose();
            timer.Stop();
            timer.Dispose();
            if (launchProcess != null)
                launchProcess.Dispose();
            picture.Image.Dispose();
        };
    }

    private void EnsureUsageAccount()
    {
        if (usageAccount != null) return;
        usageAccount = new UsageAccountWindow();
        usageAccount.Updated += delegate(UsageSnapshot snapshot) {
            if (!IsDisposed) usage.Snapshot = snapshot;
        };
    }

    private async Task RefreshUsage(bool force)
    {
        if (IsDisposed || refreshingUsage || WindowState == FormWindowState.Minimized) return;
        TimeSpan delay = usage.Snapshot.State == UsageState.Free || usage.Snapshot.State == UsageState.Pro
            ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(1);
        if (!force && DateTime.UtcNow - lastUsageAttempt < delay) return;
        lastUsageAttempt = DateTime.UtcNow;
        refreshingUsage = true;
        usage.Refreshing = true;
        try {
            EnsureUsageAccount();
            await usageAccount.RefreshUsageAsync();
        }
        finally {
            refreshingUsage = false;
            if (!IsDisposed) usage.Refreshing = false;
        }
    }
    private void SetState(string kind, string text, string description)
    {
        status.Kind = kind;
        status.Caption = text;
        status.AccessibleName = text;
        status.Invalidate();
        detail.Text = description;
        connect.Text = kind == "running" ? "Disconnect" : kind == "starting" ? "Connecting..." : kind == "stopping" ? "Disconnecting..." : "Connect";
        connect.Enabled = !busy && kind != "checking" && kind != "starting" && kind != "stopping";
        connect.ForeColor = kind == "running" ? Color.FromArgb(163, 38, 30) : SystemColors.ControlText;
        foot.Text = kind == "running" ? "Closing keeps Desktop Commander Remote running."
            : kind == "starting" ? "Desktop Commander Remote is starting."
            : kind == "stopping" ? "Desktop Commander Remote is stopping."
            : kind == "checking" ? "Checking Desktop Commander Remote."
            : kind == "error" ? "Desktop Commander Remote is unchanged."
            : "Desktop Commander Remote is stopped.";
        terminal.Enabled = kind == "running" || kind == "starting";
    }

    private async Task RefreshStatus()
    {
        if (busy || IsDisposed)
            return;
        busy = true;
        try
        {
            var agents = await Task.Run(() => Program.Agents());
            if (IsDisposed)
                return;
            running = agents.Count > 0;
            if (running)
            {
                connecting = false;
                failedStart = false;
                SetState("running", "Running on this PC", "Desktop Commander Remote is active.");
            }
            else if (connecting && launchProcess != null && !launchProcess.HasExited)
                SetState("starting", "Starting...", DateTime.UtcNow.Subtract(launchTime).TotalSeconds > 90 ? "Open the terminal for progress or sign-in instructions." : "Opening Desktop Commander Remote. Please wait.");
            else
            {
                if (connecting)
                {
                    failedStart = true;
                    connecting = false;
                }

                SetState("stopped", "Not running on this PC", failedStart ? "Desktop Commander Remote did not start. You can try Connect again." : "Connect to start Desktop Commander Remote.");
            }
        }
        catch (Exception)
        {
            if (!IsDisposed)
                SetState("error", "Unable to check status", "Desktop Commander Remote is unchanged. Please try again shortly.");
        }
        finally
        {
            busy = false;
            if (!IsDisposed)
                connect.Enabled = status.Kind == "running" || status.Kind == "stopped";
        }
    }

    private async Task ChangeConnection(bool quiet)
    {
        if (busy || connecting)
            return;
        // Keep the requested action if another launcher changes the process list meanwhile.
        bool wantConnection = !running;
        busy = true;
        connect.Enabled = false;
        try
        {
            var agents = await Task.Run(() => Program.Agents());
            if (wantConnection && agents.Count > 0)
                return;
            if (!wantConnection && agents.Count == 0)
                return;
            if (!wantConnection)
            {
                SetState("stopping", "Disconnecting...", "Stopping Desktop Commander Remote.");
                foreach (var agent in agents)
                {
                    int exit = await Task.Run(() =>
                    {
                        using (var helper = Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--disconnect " + agent.Id + " " + agent.Created) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
                        {
                            if (!helper.WaitForExit(10000))
                                return 8;
                            return helper.ExitCode;
                        }
                    });
                    if (exit != 0)
                        throw new InvalidOperationException("Desktop Commander Remote could not be disconnected safely. Use Ctrl+C in its command window.");
                }

                for (int i = 0; i < 20; i++)
                {
                    if ((await Task.Run(() => Program.Agents())).Count == 0)
                        return;
                    await Task.Delay(400);
                }

                throw new InvalidOperationException("Desktop Commander Remote is still stopping. Check its command window before trying again.");
            }

            if (!File.Exists(Program.Launcher))
                throw new FileNotFoundException("The Desktop Commander Remote launcher is missing from the scripts folder.");
            failedStart = false;
            if (launchProcess != null)
                launchProcess.Dispose();
            // Keep a real, dedicated console for Ctrl+C and on-demand terminal access, but hide it by default.
            launchProcess = Native.Launch(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/d /s /c \"\"" + Program.Launcher + "\"\"", 0, Program.TerminalColumns);
            launchTime = DateTime.UtcNow;
            connecting = true;
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !quiet)
                MessageBox.Show(this, ex.Message, Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            busy = false;
            if (!IsDisposed)
                BeginInvoke(new Action(async delegate
                {
                    await RefreshStatus();
                }));
        }
    }

    private async Task OpenTerminal()
    {
        terminal.Enabled = false;
        try
        {
            var agents = await Task.Run(() => Program.Agents());
            Agent target = agents.FirstOrDefault();
            if (target == null && launchProcess != null && !launchProcess.HasExited)
                target = new Agent
                {
                    Id = launchProcess.Id,
                    Created = launchProcess.StartTime.ToUniversalTime().Ticks
                };
            if (target == null)
            {
                await RefreshStatus();
                return;
            }

            // This separate GUI reads the existing console. Closing it never signals or owns the agent.
            using (var viewer = Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--terminal-view " + target.Id + " " + target.Created) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Normal }))
            {
            }
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
                MessageBox.Show(this, "The terminal could not be opened.\r\n" + ex.Message, Program.Title);
        }
        finally
        {
            if (!IsDisposed)
                terminal.Enabled = running || connecting;
        }
    }
}
