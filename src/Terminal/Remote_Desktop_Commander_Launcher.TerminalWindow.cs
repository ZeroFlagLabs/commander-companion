using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// This GUI is only a view of the existing console. Disposing it detaches without
// closing the console window, sending Ctrl+C, or terminating any target process.
internal sealed class TerminalWindow : Form
{
    private readonly ConsoleReader reader;
    private readonly Agent target;
    private readonly TerminalOutputBox output = new TerminalOutputBox();
    private readonly TextBox input = new TextBox();
    private readonly Button send = new Button();
    private readonly Label notice = new Label();
    private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    private bool bannerWidthFitted;
    internal TerminalWindow(Agent agent)
    {
        target = agent;
        reader = new ConsoleReader(agent);
        Text = Program.Title + " - Terminal";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Font = new Font("Segoe UI", 9F);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(820, 470);
        MinimumSize = new Size(520, 310);
        StartPosition = FormStartPosition.CenterScreen;
        output.Dock = DockStyle.Fill;
        output.Font = new Font("Consolas", 10F);
        output.BackColor = Color.FromArgb(22, 22, 24);
        output.ForeColor = Color.Gainsboro;
        output.BorderStyle = BorderStyle.None;
        output.DetectUrls = false;
        output.AccessibleName = "Commander terminal output";
        var entry = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 43,
            Padding = new Padding(8),
            ColumnCount = 2
        };
        entry.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        entry.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        input.Dock = DockStyle.Fill;
        input.AccessibleName = "Terminal input";
        input.KeyDown += delegate (object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendInput();
            }
        };
        send.Text = "Send";
        send.Dock = DockStyle.Fill;
        send.Click += delegate
        {
            SendInput();
        };
        entry.Controls.Add(input, 0, 0);
        entry.Controls.Add(send, 1, 0);
        notice.Dock = DockStyle.Bottom;
        notice.Height = 35;
        notice.Padding = new Padding(10, 0, 10, 0);
        notice.TextAlign = ContentAlignment.MiddleLeft;
        notice.Text = "Closing this window keeps Desktop Commander Remote running. Use Disconnect to stop it.";
        Controls.Add(output);
        Controls.Add(entry);
        Controls.Add(notice);
        // Load occurs after the form's initial DPI scaling and docking. Measure
        // with the same rich-edit/RTF renderer, not a separate GDI estimate.
        Load += delegate
        {
            FitRenderedBanner();
        };
        timer.Interval = 500;
        timer.Tick += delegate
        {
            RefreshOutput();
        };
        FormClosed += delegate
        {
            timer.Stop();
            timer.Dispose();
            reader.Dispose();
        };
        RefreshOutput();
        timer.Start();
    }

    private void FitRenderedBanner()
    {
        if (bannerWidthFitted || !IsHandleCreated || output.TextLength < 2)
            return;
        PerformLayout();
        int bannerPixels = 0;
        string text = output.Text;
        // Measure the actual rich-edit character advance after DPI scaling.
        // A separate font measurement can overestimate the required width.
        for (int start = 0; start < Math.Min(text.Length - 1, 2048); start++)
        {
            int count = 0;
            while (count < 32 && start + count < text.Length && text[start + count] >= ' ' && text[start + count] <= '~')
                count++;
            if (count < 8)
                continue;
            Point first = output.GetPositionFromCharIndex(start), last = output.GetPositionFromCharIndex(start + count - 1);
            if (first.Y != last.Y || last.X <= first.X)
                continue;
            bannerPixels = (int)Math.Ceiling((last.X - first.X) * (double)Program.BannerColumns / (count - 1));
            break;
        }

        if (bannerPixels <= 0)
            return;
        int allowance = Math.Max(SystemInformation.VerticalScrollBarWidth, ClientSize.Width - output.ClientSize.Width) + 8;
        int available = Math.Max(520, Screen.FromControl(this).WorkingArea.Width - 32);
        ClientSize = new Size(Math.Min(available, Math.Max(520, bannerPixels + allowance)), ClientSize.Height);
        PerformLayout();
        output.Select(output.TextLength, 0);
        output.ScrollToCaret();
        bannerWidthFitted = true;
    }

    private void SendInput()
    {
        try
        {
            reader.SendLine(input.Text);
            input.Clear();
            input.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Program.Title);
        }
    }

    private void RefreshOutput()
    {
        try
        {
            ConsoleFrame frame = reader.CaptureFrame();
            if (output.BackColor != frame.Background)
                output.BackColor = frame.Background;
            output.ApplyFrame(frame);
            if (Visible && !bannerWidthFitted)
                FitRenderedBanner();
            using (var p = Process.GetProcessById(target.Id))
                if (p.StartTime.ToUniversalTime().Ticks != target.Created || p.HasExited)
                    SessionEnded();
        }
        catch (ArgumentException)
        {
            SessionEnded();
        }
        catch (IOException)
        { /* A concurrent console resize is retried on the next tick. */
        }
        catch (InvalidOperationException)
        {
            SessionEnded();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            SessionEnded();
        }
    }

    private void SessionEnded()
    {
        timer.Stop();
        input.Enabled = false;
        send.Enabled = false;
        notice.Text = "This Desktop Commander Remote session has stopped. You can close this window.";
    }
}
