using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("Commander Companion — Usage Preview")]
[assembly: AssemblyDescription("Interactive Free, Pro and signed-out usage examples; sample data only")]
[assembly: AssemblyVersion("1.0.0.0")]

internal static class PreviewProgram
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (var window = new UsagePreviewWindow())
        {
            if (args.Length == 2 && args[0] == "--render-preview")
            {
                // Off-screen rendering never starts Commander or opens a browser.
                string folder = Path.GetFullPath(args[1]);
                Directory.CreateDirectory(folder);
                window.ShowInTaskbar = false;
                window.StartPosition = FormStartPosition.Manual;
                window.Location = new Point(-20000, -20000);
                window.Show();
                Application.DoEvents();
                for (int i = 0; i < UsagePreviewWindow.Examples.Length; i++)
                {
                    window.SelectExample(i);
                    window.PerformLayout();
                    Application.DoEvents();
                    using (var bitmap = new Bitmap(window.Width, window.Height))
                    {
                        window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        bitmap.Save(Path.Combine(folder, "usage-preview-" + i + ".png"));
                    }
                }
                window.Close();
                return 0;
            }
            Application.Run(window);
        }
        return 0;
    }
}

internal sealed class UsagePreviewWindow : Form
{
    internal static readonly string[] Examples = {
        "Free — 3,340 of 10,000", "Pro — unlimited", "Signed out",
        "Free — nearly at the limit", "Free — allowance reached",
        "Usage temporarily unavailable"
    };

    private readonly StatusView status = new StatusView();
    private readonly Label detail = new Label();
    private readonly Label foot = new Label();
    private readonly Button connect = new Button();
    private readonly Button terminal = new Button();
    private readonly ComboBox examples = new ComboBox();
    private readonly UsageCard usage = new UsageCard();
    private readonly ToolTip tips = new ToolTip();
    private readonly PictureBox picture = new PictureBox();
    private bool running = true;

    // A preview can open without taking focus from the user's current app.
    protected override bool ShowWithoutActivation { get { return true; } }

    internal UsagePreviewWindow()
    {
        Text = "Commander Companion — Usage preview";
        Font = new Font("Segoe UI", 10F);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(480, 479);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MinimizeBox = true;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(247, 248, 250);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        picture.SizeMode = PictureBoxSizeMode.Zoom;
        picture.Bounds = new Rectangle(27, 23, 43, 50);
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LauncherLogo"))
        using (var image = Image.FromStream(stream)) picture.Image = new Bitmap(image);
        var heading = new Label {
            Text = "Commander Companion", Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            Bounds = new Rectangle(83, 22, 333, 29)
        };
        var subtitle = new Label {
            Text = "Connect this PC to ChatGPT via Desktop Commander Remote", ForeColor = Color.DimGray,
            Bounds = new Rectangle(84, 53, 360, 23), Font = new Font("Segoe UI", 9F)
        };
        var about = new AboutButton { Bounds = new Rectangle(428, 23, 26, 26), BackColor = BackColor };
        tips.SetToolTip(about, "About and updates — preview only");
        about.Click += delegate { MessageBox.Show(this,
            "Commander Companion by ZeroFlagLabs.\r\n\r\nThis is a sample-data usage preview. The real launcher's About window includes version information, the GitHub project and update checks.",
            "About Commander Companion — Preview", MessageBoxButtons.OK, MessageBoxIcon.Information); };
        status.SetBounds(20, 99, 440, 39);
        detail.SetBounds(26, 146, 428, 42);
        detail.ForeColor = Color.FromArgb(65, 70, 78);
        detail.TextAlign = ContentAlignment.TopCenter;

        connect.SetBounds(26, 201, 134, 37);
        connect.Click += delegate { running = !running; UpdateConnectionExample(); };
        tips.SetToolTip(connect, "Preview only: changes the example status. Your real connection is untouched.");
        terminal.Text = "Open terminal";
        terminal.SetBounds(173, 201, 134, 37);
        terminal.Click += delegate {
            detail.Text = "This is a preview. Your real terminal is unchanged.";
        };
        tips.SetToolTip(terminal, "Preview only: no terminal is opened.");
        var minimise = new Button {
            Text = "Minimise", Bounds = new Rectangle(320, 201, 134, 37)
        };
        minimise.Click += delegate { WindowState = FormWindowState.Minimized; };

        usage.SetBounds(26, 253, 428, 108);
        foot.SetBounds(26, 376, 294, 25);
        foot.Font = new Font("Segoe UI", 9F);
        foot.ForeColor = Color.DimGray;
        foot.TextAlign = ContentAlignment.MiddleLeft;
        foot.AutoEllipsis = true;
        var startup = new CheckBox {
            Text = "Start with Windows", Font = new Font("Segoe UI", 9F), AutoSize = true
        };
        startup.Size = startup.PreferredSize;
        startup.Location = new Point(ClientSize.Width - 26 - startup.Width, 379);
        tips.SetToolTip(startup, "Preview only: this checkbox does not change Windows startup.");

        // Example controls sit outside the proposed app layout.
        var rule = new Label {
            BackColor = Color.FromArgb(222, 226, 232), Bounds = new Rectangle(0, 414, 480, 1)
        };
        var exampleCaption = new Label {
            Text = "Preview", Bounds = new Rectangle(26, 430, 62, 24),
            Font = new Font("Segoe UI", 9F), TextAlign = ContentAlignment.MiddleLeft
        };
        examples.DropDownStyle = ComboBoxStyle.DropDownList;
        examples.Font = new Font("Segoe UI", 9F);
        examples.SetBounds(92, 429, 362, 26);
        examples.Items.AddRange(Examples);
        examples.SelectedIndexChanged += delegate { usage.Example = examples.SelectedIndex; };
        var disclaimer = new Label {
            Text = "Sample data. Connection buttons and startup checkbox are simulated.",
            Bounds = new Rectangle(26, 458, 428, 17), Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(103, 110, 122)
        };
        Controls.AddRange(new Control[] {
            picture, heading, subtitle, about, status, detail, connect, terminal, minimise,
            usage, foot, startup, rule, exampleCaption, examples, disclaimer
        });
        UpdateConnectionExample();
        examples.SelectedIndex = 0;
        FormClosed += delegate { picture.Image.Dispose(); tips.Dispose(); };
    }

    internal void SelectExample(int index) { examples.SelectedIndex = index; }

    private void UpdateConnectionExample()
    {
        status.Kind = running ? "running" : "stopped";
        status.Caption = running ? "Running on this PC" : "Not running on this PC";
        status.AccessibleName = status.Caption;
        status.Invalidate();
        detail.Text = running ? "Desktop Commander Remote is active." : "Connect to start Desktop Commander Remote.";
        connect.Text = running ? "Disconnect" : "Connect";
        connect.ForeColor = running ? Color.FromArgb(163, 38, 30) : SystemColors.ControlText;
        terminal.Enabled = running;
        foot.Text = running ? "Closing keeps Desktop Commander Remote running." : "Desktop Commander Remote is stopped.";
    }
}

internal sealed class UsageCard : Panel
{
    private readonly LinkLabel accountLink = new LinkLabel();
    private readonly Font small = new Font("Segoe UI", 9F);
    private readonly Font valueFont = new Font("Segoe UI", 21F, FontStyle.Bold);
    private readonly Font messageFont = new Font("Segoe UI", 16F, FontStyle.Bold);
    private readonly Font badgeFont = new Font("Segoe UI", 8F, FontStyle.Bold);
    private int example;
    private static readonly Color Blue = Color.FromArgb(0, 120, 215);
    private static readonly Color Ink = Color.FromArgb(30, 38, 51);
    private static readonly Color Muted = Color.FromArgb(102, 111, 125);

    internal int Example
    {
        get { return example; }
        set {
            example = value;
            accountLink.Text = example == 2 ? "Sign in" : "View usage";
            AccessibleName = "Monthly usage example: " + UsagePreviewWindow.Examples[example];
            Invalidate();
        }
    }

    internal UsageCard()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(247, 248, 250);
        AccessibleRole = AccessibleRole.Grouping;
        accountLink.Font = new Font("Segoe UI", 8.5F);
        accountLink.LinkColor = Blue;
        accountLink.ActiveLinkColor = Color.FromArgb(0, 80, 160);
        accountLink.VisitedLinkColor = Blue;
        accountLink.LinkBehavior = LinkBehavior.HoverUnderline;
        accountLink.BackColor = Color.White;
        accountLink.TextAlign = ContentAlignment.MiddleRight;
        accountLink.AccessibleDescription = "Open Desktop Commander usage in your browser and sign in if prompted.";
        accountLink.LinkClicked += delegate {
            try {
                // Open the vendor page only when the user clicks the link.
                Process.Start(new ProcessStartInfo("https://mcp.desktopcommander.app/usage") { UseShellExecute = true });
            }
            catch (Exception ex) {
                MessageBox.Show(FindForm(), "The usage page could not be opened.\r\n" + ex.Message,
                    "Desktop Commander usage", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        Controls.Add(accountLink);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        float scale = DeviceDpi / 96F;
        accountLink.SetBounds((int)(Width - 112 * scale), (int)(80 * scale),
            (int)(96 * scale), (int)(21 * scale));
    }

    private static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void TextAt(Graphics graphics, string text, Font font, Color colour, Rectangle bounds)
    {
        TextRenderer.DrawText(graphics, text, font, bounds, colour,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = DeviceDpi / 96F;
        g.ScaleTransform(scale, scale);
        float width = Width / scale, height = Height / scale;
        using (var shape = Rounded(new RectangleF(0.5F, 0.5F, width - 1, height - 1), 10))
        using (var white = new SolidBrush(Color.White))
        using (var border = new Pen(Color.FromArgb(221, 226, 234))) {
            g.FillPath(white, shape);
            g.DrawPath(border, shape);
        }
        // Draw text in device coordinates, so native text and custom geometry
        // use the same DPI scale without scaling a font twice.
        g.ResetTransform();
        Func<int, int> px = n => (int)Math.Round(n * scale);
        Func<int, int, int, int, Rectangle> box = (x, y, w, h) => new Rectangle(px(x), px(y), px(w), px(h));
        TextAt(g, "Tool calls this month", small, Muted, box(16, 7, 270, 23));

        bool pro = example == 1;
        bool signedOut = example == 2;
        bool unavailable = example == 5;
        if (!signedOut && !unavailable) {
            var badge = new RectangleF(Width - px(62), px(10), px(46), px(20));
            using (var shape = Rounded(badge, px(7)))
            using (var fill = new SolidBrush(pro ? Color.FromArgb(235, 230, 255) : Color.FromArgb(230, 242, 255))) {
                g.FillPath(fill, shape);
            }
            var label = new Rectangle((int)badge.X, (int)badge.Y, (int)badge.Width, (int)badge.Height);
            TextRenderer.DrawText(g, pro ? "PRO" : "FREE", badgeFont, label,
                pro ? Color.FromArgb(104, 68, 184) : Color.FromArgb(0, 101, 181),
                TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        string reset = NextReset();
        if (pro) {
            TextAt(g, "Unlimited", valueFont, Ink, box(16, 30, 370, 36));
            TextAt(g, "No monthly tool-call limit", small, Color.FromArgb(31, 117, 80), box(16, 64, 370, 18));
            TextAt(g, "3,340 calls this month", small, Muted, box(16, 82, 270, 18));
        }
        else if (signedOut || unavailable) {
            TextAt(g, signedOut ? "Sign in to see usage" : "Usage unavailable", messageFont,
                signedOut ? Ink : Muted, box(16, 32, 395, 33));
            TextAt(g, signedOut ? "Open your account in your browser." : "Try again later, or view your account online.",
                small, Muted, box(16, 62, 395, 18));
            TextAt(g, signedOut ? "Your connection stays running." : "Your connection is unaffected.",
                small, Muted, box(16, 82, 280, 18));
        }
        else {
            int used = example == 3 ? 9240 : example == 4 ? 10000 : 3340;
            string number = used.ToString("N0", CultureInfo.GetCultureInfo("en-GB"));
            TextAt(g, number, valueFont, Ink, box(16, 30, 395, 36));
            int numberWidth = TextRenderer.MeasureText(g, number, valueFont, new Size(1000, 1000),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
            TextAt(g, "/ 10,000", small, Muted,
                new Rectangle(px(16) + numberWidth + px(8), px(39), px(120), px(25)));
            Color colour = used >= 10000 ? Color.FromArgb(191, 58, 49)
                : used >= 8000 ? Color.FromArgb(194, 125, 20) : Blue;
            Progress(g, scale, width, used / 10000F, colour);
            string note = used == 10000 ? "Allowance reached" : Math.Floor(used / 100D).ToString(CultureInfo.InvariantCulture) + "% used";
            TextAt(g, note + " · " + reset, small, Muted, box(16, 82, 290, 18));
        }
    }

    private static void Progress(Graphics g, float scale, float width, float fraction, Color colour)
    {
        var track = new RectangleF(16 * scale, 71 * scale, (width - 32) * scale, 5 * scale);
        using (var shape = Rounded(track, 2.5F * scale))
        using (var brush = new SolidBrush(Color.FromArgb(234, 238, 244))) g.FillPath(brush, shape);
        track.Width *= fraction;
        using (var shape = Rounded(track, 2.5F * scale))
        using (var brush = new SolidBrush(colour)) g.FillPath(brush, shape);
    }

    private static string NextReset()
    {
        var now = DateTime.UtcNow;
        var next = new DateTime(now.Year, now.Month, 1).AddMonths(1);
        return "Resets " + next.ToString("d MMM", CultureInfo.GetCultureInfo("en-GB"));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) {
            small.Dispose(); valueFont.Dispose(); messageFont.Dispose(); badgeFont.Dispose();
            accountLink.Font.Dispose();
        }
        base.Dispose(disposing);
    }
}
