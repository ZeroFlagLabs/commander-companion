using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        internal int cb;
        internal string reserved, desktop, title;
        internal int x, y, xSize, ySize, xCount, yCount, fill, flags;
        internal short show, reservedSize;
        internal IntPtr reserved2, stdin, stdout, stderr;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo
    {
        internal IntPtr process, thread;
        internal int pid, tid;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string application, StringBuilder command, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string cwd, ref StartupInfo startup, out ProcessInfo info);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint pid);
    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();
    [DllImport("kernel32.dll")]
    private static extern uint GetConsoleProcessList([Out] uint[] list, uint count);
    [DllImport("kernel32.dll")]
    private static extern bool GenerateConsoleCtrlEvent(uint type, uint group);
    internal delegate bool Handler(uint type);
    [DllImport("kernel32.dll")]
    internal static extern bool SetConsoleCtrlHandler(Handler handler, bool add);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr window, int command);
    internal static Process Launch(string exe, string args, short show, int columns = 0)
    {
        var si = new StartupInfo
        {
            cb = Marshal.SizeOf(typeof(StartupInfo)),
            flags = 1 | (columns > 0 ? 0x8 : 0), // STARTF_USESHOWWINDOW | STARTF_USECOUNTCHARS
            xCount = columns,
            yCount = columns > 0 ? 3000 : 0,
            show = show,
            title = Program.Title
        };
        ProcessInfo info;
        if (!CreateProcess(exe, new StringBuilder("\"" + exe + "\" " + args), IntPtr.Zero, IntPtr.Zero, false, 0x10, IntPtr.Zero, Program.Folder, ref si, out info))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            return Process.GetProcessById(info.pid);
        }
        finally
        {
            CloseHandle(info.process);
            CloseHandle(info.thread);
        }
    }

    internal static HashSet<int> RelatedProcesses(int target)
    {
        var parents = new Dictionary<int, int>();
        var names = new Dictionary<int, string>();
        using (var search = new ManagementObjectSearcher("SELECT ProcessId, ParentProcessId, Name FROM Win32_Process"))
        using (var rows = search.Get())
            foreach (ManagementObject row in rows)
                using (row)
                {
                    int id = Convert.ToInt32(row["ProcessId"]);
                    parents[id] = Convert.ToInt32(row["ParentProcessId"]);
                    names[id] = Convert.ToString(row["Name"]);
                }

        var allowed = new HashSet<int>
        {
            target,
            Process.GetCurrentProcess().Id
        };
        int ancestor = target;
        while (parents.ContainsKey(ancestor))
        {
            ancestor = parents[ancestor];
            if (!names.ContainsKey(ancestor) || !new[]
            {
                "node.exe",
                "cmd.exe",
                "powershell.exe",
                "pwsh.exe"
            }.Contains(names[ancestor], StringComparer.OrdinalIgnoreCase))
                break;
            if (!allowed.Add(ancestor))
                break;
        }

        var descendants = new HashSet<int>
        {
            target
        };
        bool added;
        do
        {
            added = false;
            foreach (var pair in parents)
                if (descendants.Contains(pair.Value) && descendants.Add(pair.Key))
                    added = true;
        }
        while (added);
        allowed.UnionWith(descendants);
        // Our independent terminal readers share the console deliberately and ignore Ctrl+C.
        // Validate both executable and exact target identity before treating one as a known client.
        long created;
        using (var p = Process.GetProcessById(target))
            created = p.StartTime.ToUniversalTime().Ticks;
        string viewArguments = "--terminal-view " + target + " " + created;
        using (var search = new ManagementObjectSearcher("SELECT ProcessId, CommandLine, ExecutablePath FROM Win32_Process WHERE Name='" + Path.GetFileName(Application.ExecutablePath).Replace("'", "''") + "'"))
        using (var rows = search.Get())
            foreach (ManagementObject row in rows)
                using (row)
                    if (string.Equals(Convert.ToString(row["ExecutablePath"]), Application.ExecutablePath, StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(Convert.ToString(row["CommandLine"]), Regex.Escape(viewArguments) + @"(?:\s|$)"))
                        allowed.Add(Convert.ToInt32(row["ProcessId"]));
        return allowed;
    }

    internal static int Interrupt(int id, long created, HashSet<int> allowed, bool send = true)
    {
        using (var p = Process.GetProcessById(id))
            if (p.StartTime.ToUniversalTime().Ticks != created)
                return 4;
        FreeConsole();
        if (!AttachConsole((uint)id))
            return 5;
        try
        {
            SetConsoleCtrlHandler(null, true);
            uint[] clients = new uint[256];
            uint count = GetConsoleProcessList(clients, (uint)clients.Length);
            if (count == 0 || count > clients.Length || !clients.Take((int)count).Contains((uint)id) || clients.Take((int)count).Any(pid => !allowed.Contains((int)pid)))
                return 6;
            using (var p = Process.GetProcessById(id))
                if (p.StartTime.ToUniversalTime().Ticks != created)
                    return 4;
            if (!send)
                return 0;
            if (!GenerateConsoleCtrlEvent(0, 0))
                return 7;
            Thread.Sleep(200);
            return 0;
        }
        finally
        {
            FreeConsole();
        }
    }

    internal static void RestoreExisting()
    {
        foreach (var p in Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName))
            using (p)
                if (p.Id != Process.GetCurrentProcess().Id && p.MainWindowHandle != IntPtr.Zero && p.MainWindowTitle == Program.Title)
                {
                    ShowWindowAsync(p.MainWindowHandle, 9);
                    SetForegroundWindow(p.MainWindowHandle);
                }
    }

    internal static void RestoreTerminal(int id, long created)
    {
        string marker = "--terminal-view " + id + " " + created;
        using (var query = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='" + Path.GetFileName(Application.ExecutablePath).Replace("'", "''") + "'"))
        using (var rows = query.Get())
            foreach (ManagementObject row in rows)
                using (row)
                {
                    if (Convert.ToString(row["CommandLine"]).IndexOf(marker, StringComparison.Ordinal) < 0)
                        continue;
                    try
                    {
                        using (var p = Process.GetProcessById(Convert.ToInt32(row["ProcessId"])))
                            if (p.Id != Process.GetCurrentProcess().Id && p.MainWindowHandle != IntPtr.Zero)
                            {
                                ShowWindowAsync(p.MainWindowHandle, 9);
                                SetForegroundWindow(p.MainWindowHandle);
                            }
                    }
                    catch (ArgumentException)
                    {
                    }
                }
    }

    internal static void AttachReader(Agent target)
    {
        using (var p = Process.GetProcessById(target.Id))
            if (p.StartTime.ToUniversalTime().Ticks != target.Created)
                throw new InvalidOperationException("That Commander session has ended.");
        FreeConsole();
        if (!AttachConsole((uint)target.Id))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "The Commander console could not be read.");
        SetConsoleCtrlHandler(null, true);
    }

    internal static void DetachReader()
    {
        FreeConsole();
    }
}
