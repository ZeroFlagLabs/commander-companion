using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Draw the information glyph so its shape and alignment do not depend on font fallback.
internal sealed class AboutButton : Button
{
    internal AboutButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        AccessibleName = "About and updates";
        AccessibleDescription = "Show the app version, credits, GitHub project and update check.";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = DeviceDpi / 96F;
        float size = 17 * scale;
        float x = (Width - size) / 2, y = (Height - size) / 2;
        using (var pen = new Pen(Color.FromArgb(102, 111, 125), 1.5F * scale))
        using (var brush = new SolidBrush(Color.FromArgb(102, 111, 125))) {
            e.Graphics.DrawEllipse(pen, x, y, size, size);
            e.Graphics.FillEllipse(brush, x + 7.5F * scale, y + 3.5F * scale, 2 * scale, 2 * scale);
            e.Graphics.FillRectangle(brush, x + 7.5F * scale, y + 7 * scale, 2 * scale, 6 * scale);
        }
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(2, 2, Width - 4, Height - 4));
    }
}
