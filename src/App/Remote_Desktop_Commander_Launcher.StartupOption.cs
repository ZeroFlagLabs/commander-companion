using System;
using System.Windows.Forms;
using Microsoft.Win32;

internal sealed class StartupOption
{
    internal const string DefaultKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    // Retain the existing startup identifier so rebranding preserves the user's setting.
    private const string ValueName = "RemoteDesktopCommander";
    private readonly string key;
    internal StartupOption(string keyPath = DefaultKey)
    {
        key = keyPath;
    }

    internal bool Enabled
    {
        get
        {
            using (var k = Registry.CurrentUser.OpenSubKey(key))
                return string.Equals(k == null ? null : k.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
        }

        set
        {
            using (var k = Registry.CurrentUser.CreateSubKey(key))
            {
                if (value)
                    k.SetValue(ValueName, Command, RegistryValueKind.String);
                else
                    k.DeleteValue(ValueName, false);
            }
        }
    }

    private static string Command
    {
        get
        {
            return "\"" + Application.ExecutablePath + "\" --startup";
        }
    }
}
