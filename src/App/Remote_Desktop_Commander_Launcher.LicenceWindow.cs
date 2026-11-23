using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal sealed class LicenceWindow : Form
{
    internal LicenceWindow()
    {
        Text = "Licences — " + AppInfo.Title;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Font = new Font("Segoe UI", 9F);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(680, 520);
        MinimumSize = new Size(480, 320);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Padding = new Padding(12, 12, 12, 0);
        var text = new RichTextBox { ReadOnly = true, WordWrap = true, BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = Color.White,
            Font = new Font("Segoe UI", 9.5F), DetectUrls = true,
            Text = File.ReadAllText(Path.Combine(AppAssets.CacheRoot, "licences", "Notices.txt")) };
        using (var heading = new Font("Segoe UI", 11F, FontStyle.Bold))
            foreach (string title in new[] { "Commander Companion — application licence", "Bundled components",
                "Microsoft WebView2 SDK — licence", "Microsoft WebView2 SDK — third-party notices" }) {
                int position = text.Find(title, RichTextBoxFinds.MatchCase);
                if (position >= 0) { text.SelectionFont = heading; text.SelectionColor = Color.FromArgb(30, 38, 51); }
            }
        text.Select(0, 0);
        text.ScrollToCaret();
        text.LinkClicked += delegate(object sender, LinkClickedEventArgs e) {
            Uri uri;
            if (!Uri.TryCreate(e.LinkText, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps) return;
            try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception) { MessageBox.Show(this, "The website could not be opened.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); }
        };
        var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 88 };
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(8) };
        footer.Controls.Add(close);
        Controls.Add(text); Controls.Add(footer);
        CancelButton = close;
    }
}
