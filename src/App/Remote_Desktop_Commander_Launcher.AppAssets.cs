using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

// The public download is one EXE. Trusted embedded support files are materialised
// in a per-user, content-addressed cache, never beside the downloaded executable.
internal static class AppAssets
{
    private static bool prepared;
    internal static string CacheRoot { get; private set; }
    internal static string LibraryFolder { get { return Path.Combine(CacheRoot, "bin"); } }

    internal static void Prepare()
    {
        if (prepared) return;
        var files = new Dictionary<string, byte[]> {
            { "bin/Microsoft.Web.WebView2.Core.dll", Read("Core") },
            { "bin/Microsoft.Web.WebView2.WinForms.dll", Read("Forms") },
            { "bin/WebView2Loader.dll", Read("Loader." + Architecture()) },
            { "resources/Remote_Desktop_Commander_Launcher.png", Read("Logo") },
            { "scripts/DesktopCommander-Remote.cmd", Read("Script") },
            { "licences/Notices.txt", Read("Notices") }
        };
        string identity;
        using (var content = new MemoryStream()) {
            foreach (var item in files) {
                byte[] name = Encoding.UTF8.GetBytes(item.Key);
                content.Write(name, 0, name.Length);
                byte[] digest = Hash(item.Value);
                content.Write(digest, 0, digest.Length);
            }
            identity = Hex(Hash(content.ToArray()));
        }
        CacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZeroFlagLabs", "CommanderCompanion", "Runtime", AppInfo.VersionText + "-" + identity.Substring(0, 16));
        using (var mutex = new Mutex(false, @"Local\ZeroFlagLabs.CommanderCompanion.Assets." + identity)) {
            bool owned = false;
            try {
                try { owned = mutex.WaitOne(15000); }
                catch (AbandonedMutexException) { owned = true; }
                if (!owned) throw new IOException("The app's support files are busy. Please try again.");
                foreach (var item in files) EnsureFile(item.Key, item.Value);
            }
            finally { if (owned) mutex.ReleaseMutex(); }
        }
        AppDomain.CurrentDomain.AssemblyResolve += ResolveLibrary;
        ConfigureLoader();
        prepared = true;
    }

    private static string Architecture()
    {
        if (!Environment.Is64BitProcess) return "x86";
        return string.Equals(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE"), "ARM64",
            StringComparison.OrdinalIgnoreCase) ? "arm64" : "x64";
    }

    private static void EnsureFile(string relative, byte[] expected)
    {
        string root = Path.GetFullPath(CacheRoot) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(CacheRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid support-file path.");
        if (File.Exists(path) && Hex(Hash(File.ReadAllBytes(path))) == Hex(Hash(expected))) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            File.WriteAllBytes(temporary, expected);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static Assembly ResolveLibrary(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name;
        if (name != "Microsoft.Web.WebView2.Core" && name != "Microsoft.Web.WebView2.WinForms") return null;
        return Assembly.LoadFrom(Path.Combine(LibraryFolder, name + ".dll"));
    }

    // Delay JIT resolution of WebView2 until our assembly resolver is registered.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ConfigureLoader()
    {
        Microsoft.Web.WebView2.Core.CoreWebView2Environment.SetLoaderDllFolderPath(LibraryFolder);
    }

    private static byte[] Read(string name)
    {
        using (var input = typeof(AppAssets).Assembly.GetManifestResourceStream("Payload." + name))
        using (var output = new MemoryStream()) {
            if (input == null) throw new IOException("An embedded support file is missing.");
            input.CopyTo(output);
            return output.ToArray();
        }
    }
    private static byte[] Hash(byte[] bytes) { using (var sha = SHA256.Create()) return sha.ComputeHash(bytes); }
    private static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }
}
