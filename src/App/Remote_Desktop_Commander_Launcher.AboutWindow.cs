using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal sealed class AboutWindow : Form
{
    private readonly ReleaseChecker updates;
    private readonly Label updateStatus = new Label();
    private readonly Button check = new Button(), download = new Button();
    private readonly Button close;
    private readonly CheckBox checkOnLaunch = new CheckBox();
    private bool loading;
    private bool arrangingUpdates;

    internal AboutWindow(ReleaseChecker checker)
    {
        updates = checker;
        Text = "About " + AppInfo.Title;
        Font = new Font("Segoe UI", 9F);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(460, 403);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(247, 248, 250);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var picture = new PictureBox { Image = Icon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom,
            Bounds = new Rectangle(24, 24, 56, 56) };
        string artwork = Path.Combine(Program.Resources, "Remote_Desktop_Commander_Launcher.png");
        if (File.Exists(artwork))
            using (var image = Image.FromFile(artwork)) {
                picture.Image.Dispose();
                picture.Image = new Bitmap(image);
            }
        var heading = new Label { Text = AppInfo.Title, Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            Bounds = new Rectangle(96, 24, 340, 30) };
        var version = new Label { Text = "Version " + AppInfo.VersionText + "  ·  ZeroFlagLabs",
            ForeColor = Color.DimGray, Bounds = new Rectangle(97, 60, 339, 22) };
        var description = new Label { Text = "A Windows launcher and usage monitor for Desktop Commander Remote.",
            Bounds = new Rectangle(24, 105, 412, 24) };
        var credits = new Label { Text = "Original application code: MIT Licence.\r\nThird-party components retain their own licences.\r\nNot affiliated with or endorsed by Desktop Commander.",
            ForeColor = Color.DimGray, Bounds = new Rectangle(24, 136, 412, 54) };
        var project = new LinkLabel { Text = "Project on GitHub", AutoSize = true,
            LinkColor = Color.FromArgb(0, 120, 215), Location = new Point(24, 199) };
        project.LinkClicked += delegate { OpenLink(this, AppInfo.ProjectUrl); };
        var licences = new LinkLabel { Text = "Licences", AutoSize = true,
            LinkColor = Color.FromArgb(0, 120, 215), Location = new Point(387, 199) };
        licences.LinkClicked += delegate { using (var dialog = new LicenceWindow()) dialog.ShowDialog(this); };
        var rule = new Label { BackColor = Color.FromArgb(222, 226, 232), Bounds = new Rectangle(24, 223, 412, 1) };
        checkOnLaunch.Text = "Check for updates at launch";
        checkOnLaunch.SetBounds(24, 239, 412, 25);
        loading = true;
        try { checkOnLaunch.Checked = updates.CheckOnLaunch; }
        catch (Exception) { checkOnLaunch.Enabled = false; }
        loading = false;
        checkOnLaunch.CheckedChanged += delegate {
            if (loading) return;
            try { updates.CheckOnLaunch = checkOnLaunch.Checked; }
            catch (Exception) {
                loading = true;
                checkOnLaunch.Checked = !checkOnLaunch.Checked;
                loading = false;
                MessageBox.Show(this, "The update preference could not be saved.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        updateStatus.AutoSize = true;
        updateStatus.MaximumSize = new Size(412, 0);
        updateStatus.Location = new Point(24, 271);
        updateStatus.ForeColor = Color.DimGray;
        updateStatus.AccessibleName = "Update check status";
        check.Text = "Check for updates";
        check.SetBounds(24, 309, 191, 33);
        check.Click += async delegate { await updates.CheckAsync(); };
        download.Text = "Open GitHub release";
        download.SetBounds(227, 309, 209, 33);
        download.Click += delegate { OpenLink(this, AppInfo.ReleasesUrl); };
        close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(348, 354, 88, 30) };
        CancelButton = close;
        Controls.AddRange(new Control[] { picture, heading, version, description, credits, project, licences, rule,
            checkOnLaunch, updateStatus, check, download, close });
        updates.Changed += UpdatesChanged;
        FormClosed += delegate { updates.Changed -= UpdatesChanged; picture.Image.Dispose(); };
        RefreshUpdateStatus();
    }

    private void UpdatesChanged(object sender, EventArgs e) { if (!IsDisposed) RefreshUpdateStatus(); }
    private void RefreshUpdateStatus()
    {
        updateStatus.Text = updates.Result.Status;
        check.Enabled = !updates.Checking;
        ArrangeUpdateControls();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        ArrangeUpdateControls();
    }

    private void ArrangeUpdateControls()
    {
        if (arrangingUpdates || close == null || check.Parent == null) return;
        arrangingUpdates = true;
        try {
            int gap = (int)System.Math.Round(12 * DeviceDpi / 96F);
            updateStatus.MaximumSize = new Size(ClientSize.Width - 2 * updateStatus.Left, 0);
            check.Top = download.Top = updateStatus.Bottom + gap;
            close.Top = check.Bottom + gap;
            ClientSize = new Size(ClientSize.Width, close.Bottom + (int)System.Math.Round(19 * DeviceDpi / 96F));
        }
        finally { arrangingUpdates = false; }
    }

    private static void OpenLink(IWin32Window owner, string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception) { MessageBox.Show(owner, "The website could not be opened. Please try again in your browser.",
            "About " + AppInfo.Title, MessageBoxButtons.OK, MessageBoxIcon.Information); }
    }
}
