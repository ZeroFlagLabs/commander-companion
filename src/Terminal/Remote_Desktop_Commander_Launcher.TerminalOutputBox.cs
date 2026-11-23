using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Reading history never changes the visible document. Keep the latest snapshot
// pending, and resume following only after the user returns to the bottom.
internal sealed class TerminalOutputBox : RichTextBox
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ScrollInfo
    {
        internal uint Size, Mask;
        internal int Minimum, Maximum;
        internal uint Page;
        internal int Position, TrackPosition;
    }

    [DllImport("user32.dll")]
    private static extern bool GetScrollInfo(IntPtr window, int bar, ref ScrollInfo info);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    private string pending;
    private string pendingRtf, appliedRtf, appliedText;
    private bool updating, scheduled;
    internal TerminalOutputBox()
    {
        ReadOnly = true;
        WordWrap = true;
        ScrollBars = RichTextBoxScrollBars.Vertical;
        VScroll += delegate
        {
            ScheduleResume();
        };
        MouseWheel += delegate
        {
            ScheduleResume();
        };
        KeyUp += delegate
        {
            ScheduleResume();
        };
        SelectionChanged += delegate
        {
            ScheduleResume();
        };
    }

    private ScrollInfo CurrentScroll()
    {
        var info = new ScrollInfo
        {
            Size = (uint)Marshal.SizeOf(typeof(ScrollInfo)),
            Mask = 0x17
        };
        GetScrollInfo(Handle, 1, ref info);
        return info;
    }

    private bool AtBottom()
    {
        var info = CurrentScroll();
        return info.Page == 0 || info.Maximum - info.Minimum < info.Page || info.Position >= info.Maximum - (int)info.Page - 1;
    }

    internal void ApplyFrame(ConsoleFrame frame)
    {
        pending = frame.Text;
        pendingRtf = frame.Rtf;
        FlushPending();
    }

    private void ScheduleResume()
    {
        if (updating || scheduled || IsDisposed || !IsHandleCreated)
            return;
        scheduled = true;
        BeginInvoke(new Action(delegate
        {
            scheduled = false;
            if (!IsDisposed && IsHandleCreated)
                FlushPending();
        }));
    }

    private void FlushPending()
    {
        if (updating || pending == null || (pending == appliedText && pendingRtf == appliedRtf) || SelectionLength != 0)
            return;
        if (TextLength > 0 && !AtBottom())
            return;
        updating = true;
        SendMessage(Handle, 0x000B, IntPtr.Zero, IntPtr.Zero); // WM_SETREDRAW: avoid a visible text reset.
        try
        {
            Rtf = pendingRtf;
            appliedRtf = pendingRtf;
            appliedText = pending;
            SelectionStart = TextLength;
            SelectionLength = 0;
            ScrollToCaret();
        }
        finally
        {
            SendMessage(Handle, 0x000B, new IntPtr(1), IntPtr.Zero);
            updating = false;
            Invalidate();
        }
    }
}
