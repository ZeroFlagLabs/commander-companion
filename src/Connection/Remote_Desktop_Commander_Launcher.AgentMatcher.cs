using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

internal static class AgentMatcher
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW(string command, out int count);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr pointer);
    private static string[] Arguments(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return new string[0];
        int count;
        IntPtr memory = CommandLineToArgvW(command, out count);
        if (memory == IntPtr.Zero)
            return new string[0];
        try
        {
            var args = new string[count];
            for (int i = 0; i < count; i++)
                args[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(memory, i * IntPtr.Size));
            return args;
        }
        finally
        {
            LocalFree(memory);
        }
    }

    private static bool Package(string text)
    {
        return Regex.IsMatch(text ?? "", @"desktop[-_]?commander", RegexOptions.IgnoreCase);
    }

    internal static bool IsRemoteLauncher(string command)
    {
        return (command ?? "").IndexOf("DesktopCommander-Remote.cmd", StringComparison.OrdinalIgnoreCase) >= 0 || (command ?? "").IndexOf("remote-windows.ps1", StringComparison.OrdinalIgnoreCase) >= 0 || (Package(command) && Arguments(command).Any(a => string.Equals(a, "remote", StringComparison.OrdinalIgnoreCase)));
    }

    internal static bool IsRemoteEntry(string command, bool knownAncestor)
    {
        string[] args = Arguments(command);
        int remote = Array.FindIndex(args, a => string.Equals(a, "remote", StringComparison.OrdinalIgnoreCase));
        if (remote < 2)
            return false;
        string[] before = args.Skip(1).Take(remote - 1).ToArray();
        if (before.Any(a => Regex.IsMatch(a, @"(?:^|[\\/])(?:npm|npx)-cli\.js$", RegexOptions.IgnoreCase)))
            return false;
        if (args.Skip(remote + 1).Any(a => new[] { "--help", "-h", "--report", "--logout" }.Contains(a, StringComparer.OrdinalIgnoreCase)))
            return false;
        return before.Any(Package) || (knownAncestor && before.Any(IndexScript));
    }

    private static bool IndexScript(string value)
    {
        try
        {
            return string.Equals(Path.GetFileName(value), "index.js", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
