using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

internal sealed class UsageCard : Panel
{
    private readonly LinkLabel accountLink = new LinkLabel();
    private readonly Button refreshButton = new RefreshIconButton();
    private readonly ToolTip tips = new ToolTip();
    private readonly Font small = new Font("Segoe UI", 9F);
    private readonly Font valueFont = new Font("Segoe UI", 21F, FontStyle.Bold);
    private readonly Font messageFont = new Font("Segoe UI", 16F, FontStyle.Bold);
    private readonly Font badgeFont = new Font("Segoe UI", 8F, FontStyle.Bold);
    private readonly Font timeFont = new Font("Segoe UI", 8F);
    private UsageSnapshot snapshot = UsageSnapshot.Status(UsageState.Loading);
    private static readonly Color Blue = Color.FromArgb(0, 120, 215);
    private static readonly Color Ink = Color.FromArgb(30, 38, 51);
    private static readonly Color Muted = Color.FromArgb(102, 111, 125);
    internal event EventHandler OpenAccount;
    internal event EventHandler RefreshRequested;

    internal bool Refreshing
    {
        get { return !refreshButton.Enabled; }
        set {
            refreshButton.Enabled = !value;
            tips.SetToolTip(refreshButton, value ? "Refreshing usage…" : "Refresh usage now");
        }
    }

    internal UsageSnapshot Snapshot
    {
        get { return snapshot; }
        set
        {
            snapshot = value ?? UsageSnapshot.Status(UsageState.Unavailable);
            accountLink.Text = snapshot.State == UsageState.SignedOut ? "Sign in" : "View usage";
            AccessibleName = "Monthly usage: " + snapshot.State;
            tips.SetToolTip(this, snapshot.UpdatedAt.HasValue
                ? "Last updated " + snapshot.UpdatedAt.Value.ToLocalTime().ToString("d MMM yyyy, HH:mm:ss", CultureInfo.GetCultureInfo("en-GB"))
                : "Usage is read from your Desktop Commander account.");
            PerformLayout();
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
        accountLink.Text = "View usage";
        accountLink.AccessibleDescription = "Open your Desktop Commander account and sign in if needed.";
        accountLink.LinkClicked += delegate { if (OpenAccount != null) OpenAccount(this, EventArgs.Empty); };
        refreshButton.ForeColor = Blue;
        refreshButton.BackColor = Color.White;
        refreshButton.UseVisualStyleBackColor = false;
        refreshButton.FlatStyle = FlatStyle.Flat;
        refreshButton.FlatAppearance.BorderSize = 0;
        refreshButton.FlatAppearance.MouseOverBackColor = Color.White;
        refreshButton.FlatAppearance.MouseDownBackColor = Color.White;
        refreshButton.Padding = Padding.Empty;
        refreshButton.Cursor = Cursors.Hand;
        refreshButton.AccessibleName = "Refresh usage";
        refreshButton.AccessibleDescription = "Check your account usage now, without changing the Commander connection.";
        refreshButton.Click += delegate { if (RefreshRequested != null) RefreshRequested(this, EventArgs.Empty); };
        tips.SetToolTip(refreshButton, "Refresh usage now");
        Controls.Add(accountLink);
        Controls.Add(refreshButton);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        float scale = DeviceDpi / 96F;
        accountLink.SetBounds((int)(Width - 112 * scale), (int)(80 * scale), (int)(96 * scale), (int)(21 * scale));
        bool known = snapshot.State == UsageState.Free || snapshot.State == UsageState.Pro;
        refreshButton.SetBounds((int)(Width - (known ? 92 : 40) * scale), (int)(9 * scale), (int)(22 * scale), (int)(22 * scale));
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

    private sealed class RefreshIconButton : Button
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            // Keep the square hit target without painting native button chrome.
            using (var background = new SolidBrush(BackColor)) e.Graphics.FillRectangle(background, ClientRectangle);
            // Draw the arrow directly so it stays crisp at taskbar-scale DPI,
            // without depending on a symbol font's padding or glyph metrics.
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = DeviceDpi / 96F;
            float cx = ClientSize.Width / 2F, cy = ClientSize.Height / 2F;
            float radius = 5.5F * scale;
            Color colour = Enabled ? ForeColor : SystemColors.GrayText;
            using (var pen = new Pen(colour, 1.5F * scale)) {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                e.Graphics.DrawArc(pen, cx - radius, cy - radius, radius * 2, radius * 2, 35, 280);
            }
            float x = cx + radius * 0.7071F, y = cy - radius * 0.7071F;
            using (var brush = new SolidBrush(colour)) e.Graphics.FillPolygon(brush, new[] {
                new PointF(x + 1.5F * scale, y + 1.5F * scale),
                new PointF(x - 2.7F * scale, y + 0.1F * scale),
                new PointF(x + 0.1F * scale, y - 2.7F * scale)
            });
            if (Focused && ShowFocusCues) {
                var focus = ClientRectangle;
                focus.Inflate(-1, -1);
                ControlPaint.DrawFocusRectangle(e.Graphics, focus, colour, BackColor);
            }
        }
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
        using (var border = new Pen(Color.FromArgb(221, 226, 234))) { g.FillPath(white, shape); g.DrawPath(border, shape); }
        g.ResetTransform();
        Func<int, int> px = n => (int)Math.Round(n * scale);
        Func<int, int, int, int, Rectangle> box = (x, y, w, h) => new Rectangle(px(x), px(y), px(w), px(h));
        TextAt(g, "Tool calls this month", small, Muted, box(16, 7, 220, 23));
        bool known = snapshot.State == UsageState.Free || snapshot.State == UsageState.Pro;
        bool pro = snapshot.State == UsageState.Pro;
        if (known)
        {
            var badge = new RectangleF(Width - px(62), px(10), px(46), px(20));
            using (var shape = Rounded(badge, px(7)))
            using (var fill = new SolidBrush(pro ? Color.FromArgb(235, 230, 255) : Color.FromArgb(230, 242, 255))) g.FillPath(fill, shape);
            TextRenderer.DrawText(g, pro ? "PRO" : "FREE", badgeFont,
                new Rectangle((int)badge.X, (int)badge.Y, (int)badge.Width, (int)badge.Height),
                pro ? Color.FromArgb(104, 68, 184) : Color.FromArgb(0, 101, 181),
                TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (snapshot.UpdatedAt.HasValue)
                TextRenderer.DrawText(g, "Updated " + snapshot.UpdatedAt.Value.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture),
                    timeFont, box((int)width - 190, 8, 90, 23), Muted,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }
        if (pro)
        {
            TextAt(g, "Unlimited", valueFont, Ink, box(16, 30, 395, 36));
            TextAt(g, "No monthly tool-call limit", small, Color.FromArgb(31, 117, 80), box(16, 64, 395, 18));
            TextAt(g, snapshot.CallsUsed.ToString("N0", CultureInfo.GetCultureInfo("en-GB")) + " calls this month", small, Muted, box(16, 82, 280, 18));
        }
        else if (snapshot.State == UsageState.Free)
        {
            string number = snapshot.CallsUsed.ToString("N0", CultureInfo.GetCultureInfo("en-GB"));
            TextAt(g, number, valueFont, Ink, box(16, 30, 395, 36));
            int numberWidth = TextRenderer.MeasureText(g, number, valueFont, new Size(1000, 1000), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
            TextAt(g, "/ " + snapshot.CallsIncluded.Value.ToString("N0", CultureInfo.GetCultureInfo("en-GB")), small, Muted,
                new Rectangle(px(16) + numberWidth + px(8), px(39), px(155), px(25)));
            double fraction = snapshot.CallsUsed / (double)snapshot.CallsIncluded.Value;
            Color colour = fraction >= 1 ? Color.FromArgb(191, 58, 49) : fraction >= 0.8 ? Color.FromArgb(194, 125, 20) : Blue;
            Progress(g, scale, width, Math.Min(1, fraction), colour);
            string note = fraction >= 1 ? "Allowance reached" : Math.Floor(fraction * 100).ToString(CultureInfo.InvariantCulture) + "% used";
            string reset = snapshot.ResetsAt.Value.UtcDateTime.ToString("d MMM", CultureInfo.GetCultureInfo("en-GB"));
            TextAt(g, note + " · Resets " + reset, small, Muted, box(16, 82, 292, 18));
        }
        else
        {
            bool signedOut = snapshot.State == UsageState.SignedOut;
            bool loading = snapshot.State == UsageState.Loading;
            TextAt(g, loading ? "Checking usage…" : signedOut ? "Sign in to see usage" : "Usage unavailable", messageFont,
                loading || signedOut ? Ink : Muted, box(16, 32, 395, 33));
            TextAt(g, loading ? "Connecting to your account." : signedOut ? "Sign in to your Desktop Commander account."
                : "Try again, or view your account online.", small, Muted, box(16, 62, 395, 18));
            TextAt(g, "Your connection is unaffected.", small, Muted, box(16, 82, 280, 18));
        }
    }

    private static void Progress(Graphics g, float scale, float width, double fraction, Color colour)
    {
        var track = new RectangleF(16 * scale, 71 * scale, (width - 32) * scale, 5 * scale);
        using (var shape = Rounded(track, 2.5F * scale))
        using (var brush = new SolidBrush(Color.FromArgb(234, 238, 244))) g.FillPath(brush, shape);
        if (fraction <= 0) return;
        track.Width = Math.Max(5 * scale, (float)(track.Width * fraction));
        using (var shape = Rounded(track, 2.5F * scale))
        using (var brush = new SolidBrush(colour)) g.FillPath(brush, shape);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { small.Dispose(); valueFont.Dispose(); messageFont.Dispose(); badgeFont.Dispose(); timeFont.Dispose(); tips.Dispose(); accountLink.Font.Dispose(); }
        base.Dispose(disposing);
    }
}
