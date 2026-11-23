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
        var text = new TextBox { Multiline = true, ReadOnly = true, WordWrap = true,
            ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = Color.White,
            Text = File.ReadAllText(Path.Combine(AppAssets.CacheRoot, "licences", "Notices.txt")) };
        var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 88 };
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(8) };
        footer.Controls.Add(close);
        Controls.Add(text); Controls.Add(footer);
        CancelButton = close;
    }
}
