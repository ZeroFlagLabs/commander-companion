using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal sealed class ConsoleReader : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Coord
    {
        internal short X, Y;
        internal Coord(short x, short y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SmallRect
    {
        internal short Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BufferInfo
    {
        internal Coord Size, Cursor;
        internal ushort Attributes;
        internal SmallRect Window;
        internal Coord Maximum;
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode, Size = 20)]
    private struct InputRecord
    {
        [FieldOffset(0)]
        internal ushort Type;
        [FieldOffset(4), MarshalAs(UnmanagedType.Bool)]
        internal bool Down;
        [FieldOffset(8)]
        internal ushort Repeat;
        [FieldOffset(10)]
        internal ushort Key;
        [FieldOffset(12)]
        internal ushort Scan;
        [FieldOffset(14)]
        internal char Character;
        [FieldOffset(16)]
        internal uint Control;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleScreenBufferInfo(SafeFileHandle handle, out BufferInfo info);
    [DllImport("kernel32.dll", EntryPoint = "ReadConsoleOutputW", ExactSpelling = true, SetLastError = true)]
    private static extern bool ReadConsoleOutput(SafeFileHandle handle, [Out] ConsoleCell[] cells, Coord size, Coord origin, ref SmallRect region);
    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedInfo
    {
        internal uint Size;
        internal Coord BufferSize, Cursor;
        internal ushort Attributes;
        internal SmallRect Window;
        internal Coord Maximum;
        internal ushort Popup;
        internal int FullScreen;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        internal uint[] Colours;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleScreenBufferInfoEx(SafeFileHandle handle, ref ExtendedInfo info);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool WriteConsoleInput(SafeFileHandle handle, InputRecord[] records, uint length, out uint written);
    private readonly SafeFileHandle screen, keyboard;
    private bool disposed;
    internal ConsoleReader(Agent target)
    {
        Native.AttachReader(target);
        screen = CreateFile("CONOUT$", 0x80000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        keyboard = CreateFile("CONIN$", 0x40000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (screen.IsInvalid || keyboard.IsInvalid)
        {
            Dispose();
            throw new System.ComponentModel.Win32Exception("Commander console access is unavailable.");
        }
    }

    internal ConsoleFrame CaptureFrame()
    {
        BufferInfo info;
        if (!GetConsoleScreenBufferInfo(screen, out info))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        int width = info.Size.X, last = Math.Min(info.Cursor.Y, info.Size.Y - 1);
        if (width < 1 || last < 0)
            return ConsoleFrame.FromCells(new ConsoleCell[0], 1, new uint[16]);
        int first = Math.Max(0, last - Math.Min(249, 200000 / width));
        int rows = last - first + 1;
        var cells = new ConsoleCell[width * rows];
        for (int row = 0; row < rows;)
        {
            int batchRows = Math.Min(rows - row, Math.Max(1, 8192 / width));
            var batch = new ConsoleCell[width * batchRows];
            var region = new SmallRect
            {
                Left = 0,
                Right = (short)(width - 1),
                Top = (short)(first + row),
                Bottom = (short)(first + row + batchRows - 1)
            };
            if (!ReadConsoleOutput(screen, batch, new Coord((short)width, (short)batchRows), new Coord(0, 0), ref region))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            if (region.Right - region.Left + 1 != width || region.Bottom - region.Top + 1 != batchRows)
                throw new IOException("Console dimensions changed during capture.");
            Array.Copy(batch, 0, cells, row * width, batch.Length);
            row += batchRows;
        }

        var palette = new ExtendedInfo
        {
            Size = (uint)Marshal.SizeOf(typeof(ExtendedInfo)),
            Colours = new uint[16]
        };
        uint[] colours = GetConsoleScreenBufferInfoEx(screen, ref palette) ? palette.Colours : ConsoleFrame.DefaultColours;
        return ConsoleFrame.FromCells(cells, width, colours);
    }

    internal void SendLine(string text)
    {
        string line = text.Replace("\r", "").Replace("\n", "") + "\r";
        if (line.Length > 4096)
            throw new InvalidOperationException("Please send a shorter line.");
        var records = new List<InputRecord>();
        foreach (char c in line)
        {
            var down = new InputRecord
            {
                Type = 1,
                Down = true,
                Repeat = 1,
                Character = c,
                Key = c == '\r' ? (ushort)13 : (ushort)0
            };
            records.Add(down);
            down.Down = false;
            records.Add(down);
        }

        uint written;
        if (!WriteConsoleInput(keyboard, records.ToArray(), (uint)records.Count, out written) || written != records.Count)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        if (screen != null)
            screen.Dispose();
        if (keyboard != null)
            keyboard.Dispose();
        Native.DetachReader();
    }
}
