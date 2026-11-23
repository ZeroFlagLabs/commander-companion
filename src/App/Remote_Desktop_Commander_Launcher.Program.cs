using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    internal const string Title = AppInfo.Title;
    // The upstream block-letter header plus timestamp occupies 153 columns.
    // Reserve space before the first output is written, not just in the viewer.
    internal const int BannerColumns = 153;
    internal const int TerminalColumns = BannerColumns + 3;
    internal static readonly string Folder = AppDomain.CurrentDomain.BaseDirectory;
    internal static string Resources { get { return Path.Combine(AppAssets.CacheRoot, "resources"); } }
    internal static string Launcher { get { return Path.Combine(AppAssets.CacheRoot, "scripts", "DesktopCommander-Remote.cmd"); } }
    internal static List<Agent> Agents()
    {
        var agents = new List<Agent>();
        bool inaccessible = false;
        var processes = new Dictionary<int, ProcessRecord>();
        using (var search = new ManagementObjectSearcher("SELECT ProcessId, ParentProcessId, Name, CommandLine FROM Win32_Process WHERE Name='node.exe' OR Name='bun.exe' OR Name='cmd.exe' OR Name='powershell.exe' OR Name='pwsh.exe'"))
        using (var results = search.Get())
            foreach (ManagementObject row in results)
                using (row)
                {
                    int id = Convert.ToInt32(row["ProcessId"]);
                    processes[id] = new ProcessRecord
                    {
                        Id = id,
                        Parent = Convert.ToInt32(row["ParentProcessId"]),
                        Name = Convert.ToString(row["Name"]),
                        Command = Convert.ToString(row["CommandLine"])
                    };
                }

        foreach (var row in processes.Values)
        {
            if (!string.Equals(row.Name, "node.exe", StringComparison.OrdinalIgnoreCase) && !string.Equals(row.Name, "bun.exe", StringComparison.OrdinalIgnoreCase))
                continue;
            bool knownAncestor = false;
            int parent = row.Parent;
            var visited = new HashSet<int>();
            while (visited.Add(parent) && processes.ContainsKey(parent))
            {
                var ancestor = processes[parent];
                if (AgentMatcher.IsRemoteLauncher(ancestor.Command))
                    knownAncestor = true;
                parent = ancestor.Parent;
            }

            if (string.IsNullOrWhiteSpace(row.Command))
            {
                if (knownAncestor)
                    inaccessible = true;
                continue;
            }

            if (!AgentMatcher.IsRemoteEntry(row.Command, knownAncestor))
                continue;
            try
            {
                using (var p = Process.GetProcessById(row.Id))
                    agents.Add(new Agent { Id = p.Id, Created = p.StartTime.ToUniversalTime().Ticks });
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
                inaccessible = true;
            }
        }

        if (agents.Count == 0 && inaccessible)
            throw new InvalidOperationException("A Commander process was found but could not be inspected. It has been left alone.");
        return agents;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try { AppAssets.Prepare(); return Run(args); }
        catch (Exception error) {
            if (args.Length == 2 && args[0].StartsWith("--check", StringComparison.Ordinal))
                File.WriteAllText(args[1], "Check failed: " + error.Message);
            else MessageBox.Show("The app's internal support files could not be prepared.\r\n" + error.Message,
                Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 9;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string[] args)
    {
        try
        {
            if (args.Length == 3 && (args[0] == "--disconnect" || args[0] == "--inspect-console"))
            {
                int id = int.Parse(args[1], CultureInfo.InvariantCulture);
                long created = long.Parse(args[2], CultureInfo.InvariantCulture);
                var agent = Agents().FirstOrDefault(a => a.Id == id && a.Created == created);
                return agent == null ? 0 : Native.Interrupt(id, created, Native.RelatedProcesses(id), args[0] == "--disconnect");
            }

            if (args.Length == 3 && args[0] == "--terminal-view")
            {
                int id = int.Parse(args[1], CultureInfo.InvariantCulture);
                long created = long.Parse(args[2], CultureInfo.InvariantCulture);
                var agent = Agents().FirstOrDefault(a => a.Id == id && a.Created == created);
                if (agent == null && !IsLauncher(id, created))
                    return 3;
                bool firstView;
                using (var viewMutex = new Mutex(true, @"Local\RemoteDesktopCommander.Terminal." + id + "." + created, out firstView))
                {
                    if (!firstView)
                    {
                        Native.RestoreTerminal(id, created);
                        return 0;
                    }

                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new TerminalWindow(new Agent { Id = id, Created = created }));
                    GC.KeepAlive(viewMutex);
                }

                return 0;
            }

            if (args.Length == 2 && args[0] == "--check")
            {
                var agents = Agents();
                File.WriteAllLines(args[1], new[] { "Version: " + typeof(Program).Assembly.GetName().Version, "Launcher exists: " + File.Exists(Launcher), "PNG exists: " + File.Exists(Path.Combine(Resources, "Remote_Desktop_Commander_Launcher.png")), "Remote agents: " + agents.Count });
                return File.Exists(Launcher) && File.Exists(Path.Combine(Resources, "Remote_Desktop_Commander_Launcher.png")) ? 0 : 2;
            }

            if (args.Length == 2 && args[0] == "--check-usage")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                return UsageCheck.Run(args[1]);
            }
            bool first;
            using (var mutex = new Mutex(true, @"Local\RemoteDesktopCommander.TaskbarWindow", out first))
            {
                if (!first)
                {
                    if (!args.Contains("--startup") && !args.Contains("--minimised"))
                        Native.RestoreExisting();
                    return 0;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new CommanderWindow(args.Contains("--startup"), args.Contains("--minimised")));
                GC.KeepAlive(mutex);
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (args.Contains("--check") || args.Contains("--check-usage"))
            {
                if (args.Length == 2)
                    File.WriteAllText(args[1], "Check failed: " + ex.Message);
                return 9;
            }

            if (args.Contains("--disconnect") || args.Contains("--inspect-console") || args.Contains("--terminal-view"))
                return 9;
            MessageBox.Show(ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static bool IsLauncher(int id, long created)
    {
        try
        {
            using (var p = Process.GetProcessById(id))
                if (p.StartTime.ToUniversalTime().Ticks != created)
                    return false;
            using (var row = new ManagementObject("Win32_Process.Handle='" + id.ToString(CultureInfo.InvariantCulture) + "'"))
                return string.Equals(Convert.ToString(row["Name"]), "cmd.exe", StringComparison.OrdinalIgnoreCase) && Convert.ToString(row["CommandLine"]).IndexOf(Launcher, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (ManagementException)
        {
            return false;
        }
    }
}
