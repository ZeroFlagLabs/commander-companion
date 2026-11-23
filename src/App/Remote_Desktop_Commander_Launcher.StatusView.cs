using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Draw icon and text in one control, using the same vertical centre.
internal sealed class StatusView : Control
{
    internal string Kind = "checking", Caption = "Checking...";
    internal StatusView()
    {
        DoubleBuffered = true;
        AccessibleRole = AccessibleRole.StaticText;
        Font = new Font("Segoe UI", 15F, FontStyle.Bold);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color colour = Kind == "running" ? Color.FromArgb(27, 120, 66) : Kind == "stopped" || Kind == "error" ? Color.FromArgb(179, 38, 30) : Color.FromArgb(139, 93, 13);
        Size size = TextRenderer.MeasureText(Caption, Font, new Size(Width - 32, Height), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        int x = Math.Max(4, (Width - size.Width - 29) / 2);
        float cy = Height / 2F;
        using (var pen = new Pen(colour, 1.8F))
        {
            e.Graphics.DrawEllipse(pen, x + 1, cy - 9, 18, 18);
            if (Kind == "running")
                e.Graphics.DrawLines(pen, new[] { new PointF(x + 5, cy), new PointF(x + 9, cy + 4), new PointF(x + 15, cy - 4) });
            else if (Kind == "stopped")
                e.Graphics.DrawLine(pen, x + 5, cy, x + 15, cy);
            else if (Kind == "error")
            {
                e.Graphics.DrawLine(pen, x + 10, cy - 5, x + 10, cy + 1);
                using (var brush = new SolidBrush(colour))
                    e.Graphics.FillEllipse(brush, x + 9, cy + 4, 2, 2);
            }
            else
            {
                e.Graphics.DrawLine(pen, x + 10, cy - 5, x + 10, cy);
                e.Graphics.DrawLine(pen, x + 10, cy, x + 14, cy + 2);
            }
        }

        TextRenderer.DrawText(e.Graphics, Caption, Font, new Rectangle(x + 29, 0, Width - x - 29, Height), colour, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}
