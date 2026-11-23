using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

// Optional Windows integration check. Uses a fresh account profile and sample
// figures; never connects, disconnects, sends console input or shows a window.
internal static class PackageSmoke
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    [STAThread]
    private static int Main(string[] args)
    {
        int exit = 1;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (var context = new ApplicationContext())
        using (var timer = new Timer { Interval = 50 }) {
            timer.Tick += async delegate {
                timer.Stop();
                try {
                    Assembly app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
                    app.GetType("AppAssets").GetMethod("Prepare", Static).Invoke(null, null);
                    string output = Path.GetFullPath(args[1]);
                    string profile = Path.GetFullPath(args[2]);
                    Directory.CreateDirectory(output);
                    Type accountType = app.GetType("UsageAccountWindow");
                    using (var account = (Form)Activator.CreateInstance(accountType, Instance, null,
                        new object[] { profile }, CultureInfo.InvariantCulture)) {
                        var task = (Task)accountType.GetMethod("RefreshUsageAsync", Instance).Invoke(account, null);
                        await task;
                        object snapshot = task.GetType().GetProperty("Result").GetValue(task, null);
                        string state = snapshot.GetType().GetField("State", Instance).GetValue(snapshot).ToString();
                        if (state != "SignedOut") throw new Exception("Fresh account profile returned " + state + ": " +
                            accountType.GetField("Diagnostic", Instance).GetValue(account));
                        Console.WriteLine("PASS: bundled WebView2 opens the official account website with a fresh, signed-out profile.");
                    }
                    Render(app, output);
                    Console.WriteLine("PASS: Free, Pro, signed-out, unavailable and About screens rendered from the packaged application without showing windows.");
                    exit = 0;
                }
                catch (Exception error) { Console.Error.WriteLine(error); }
                finally { context.ExitThread(); }
            };
            timer.Start();
            Application.Run(context);
        }
        return exit;
    }

    private static void Render(Assembly app, string output)
    {
        Type type = app.GetType("CommanderWindow");
        using (var window = (Form)Activator.CreateInstance(type, Instance, null,
            new object[] { false, true }, CultureInfo.InvariantCulture)) {
            foreach (string name in new[] { "timer", "usageTimer" })
                ((Timer)type.GetField(name, Instance).GetValue(window)).Stop();
            window.WindowState = FormWindowState.Normal;
            window.ShowInTaskbar = false;
            type.GetMethod("SetState", Instance | BindingFlags.DeclaredOnly).Invoke(window,
                new object[] { "running", "Running on this PC", "Desktop Commander Remote is active." });
            Control usage = (Control)type.GetField("usage", Instance).GetValue(window);
            var parser = app.GetType("UsageParser").GetMethod("Parse", Static);
            string[] json = {
                "{\"usage\":{\"plan\":\"free\",\"callsUsed\":3340,\"callsIncluded\":10000,\"resetsAt\":\"2026-12-01T00:00:00Z\"}}",
                "{\"usage\":{\"plan\":\"pro\",\"callsUsed\":18420,\"callsIncluded\":null}}"
            };
            for (int i = 0; i < json.Length; i++) {
                object snapshot = parser.Invoke(null, new object[] { json[i], new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero) });
                usage.GetType().GetProperty("Snapshot", Instance).SetValue(usage, snapshot, null);
                Save(window, Path.Combine(output, i == 0 ? "free.png" : "pro.png"));
            }
            Type stateType = app.GetType("UsageState");
            foreach (string state in new[] { "SignedOut", "Unavailable" }) {
                object snapshot = app.GetType("UsageSnapshot").GetMethod("Status", Static).Invoke(null,
                    new[] { Enum.Parse(stateType, state) });
                usage.GetType().GetProperty("Snapshot", Instance).SetValue(usage, snapshot, null);
                Save(window, Path.Combine(output, state.ToLowerInvariant() + ".png"));
            }
            var footer = (Control)type.GetField("foot", Instance).GetValue(window);
            var startup = (Control)type.GetField("startWithWindows", Instance).GetValue(window);
            if (TextRenderer.MeasureText(footer.Text, footer.Font).Width > footer.Width || footer.Right >= startup.Left)
                throw new Exception("The full service name does not fit in the footer.");
            var updates = type.GetField("updates", Instance).GetValue(window);
            Type aboutType = app.GetType("AboutWindow");
            using (var about = (Form)Activator.CreateInstance(aboutType, Instance, null,
                new[] { updates }, CultureInfo.InvariantCulture)) {
                foreach (string message in new[] { "You are up to date.",
                    "This is a deliberately long update status message that wraps to a second line to verify the spacing between the status, update buttons and Close." }) {
                    object info = app.GetType("ReleaseInfo").GetMethod("Message", Static).Invoke(null, new object[] { message });
                    updates.GetType().GetProperty("Result", Instance).SetValue(updates, info, null);
                    aboutType.GetMethod("RefreshUpdateStatus", Instance).Invoke(about, null);
                    var status = (Control)aboutType.GetField("updateStatus", Instance).GetValue(about);
                    var button = (Control)aboutType.GetField("check", Instance).GetValue(about);
                    var close = (Control)aboutType.GetField("close", Instance).GetValue(about);
                    var open = (Control)aboutType.GetField("download", Instance).GetValue(about);
                    if (button.Top - status.Bottom != close.Top - button.Bottom || !open.Enabled)
                        throw new Exception("Update controls have unequal vertical gaps or the release link is disabled.");
                }
                object ready = app.GetType("ReleaseInfo").GetMethod("Message", Static).Invoke(null, new object[] { "You are up to date." });
                updates.GetType().GetProperty("Result", Instance).SetValue(updates, ready, null);
                aboutType.GetMethod("RefreshUpdateStatus", Instance).Invoke(about, null);
                Save(about, Path.Combine(output, "about.png"));
            }
            using (var licences = (Form)Activator.CreateInstance(app.GetType("LicenceWindow"), true)) {
                var text = (TextBoxBase)licences.Controls[0];
                if (!text.Text.Contains("Microsoft Corporation") || !text.Text.Contains("MIT License"))
                    throw new Exception("Embedded licence notices are incomplete.");
                if (text.Text.Contains("# Third-party components") || text.Text.Contains("**1.0.4370.26**"))
                    throw new Exception("Markdown formatting leaked into the native licence viewer.");
                licences.CreateControl(); licences.PerformLayout();
                var rich = (RichTextBox)text;
                int separator = rich.Text.IndexOf(new string('=', 72), StringComparison.Ordinal);
                if (separator < 0 || rich.GetLineFromCharIndex(separator) != rich.GetLineFromCharIndex(separator + 71))
                    throw new Exception("The licence separator wraps at the default window width.");
                Save(licences, Path.Combine(output, "licences.png"));
            }
            ((IDisposable)updates).Dispose();
        }
    }

    private static void Save(Form window, string path)
    {
        window.CreateControl(); window.PerformLayout();
        using (var image = new Bitmap(window.Width, window.Height)) {
            window.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
            int border = (window.Width - window.ClientSize.Width) / 2;
            int top = window.Height - window.ClientSize.Height - border;
            using (var graphics = Graphics.FromImage(image)) DrawChildren(window, graphics, new Point(border, top));
            image.Save(path);
        }
    }
    private static void DrawChildren(Control parent, Graphics graphics, Point origin)
    {
        foreach (Control control in parent.Controls) {
            if (control.Width < 1 || control.Height < 1) continue;
            var point = new Point(origin.X + control.Left, origin.Y + control.Top);
            using (var image = new Bitmap(control.Width, control.Height)) {
                var rich = control as RichTextBox;
                if (rich != null) PrintRichText(rich, image);
                else control.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                graphics.DrawImageUnscaled(image, point);
            }
            DrawChildren(control, graphics, point);
        }
    }

    // RichTextBox does not implement DrawToBitmap; use its native print path.
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct CharacterRange { internal int First, Last; }
    [StructLayout(LayoutKind.Sequential)]
    private struct FormatRange { internal IntPtr Device, Target; internal NativeRectangle Area, Page; internal CharacterRange Text; }
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr PrintMessage(IntPtr window, int message, IntPtr parameter, ref FormatRange range);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr ClearPrintCache(IntPtr window, int message, IntPtr parameter, IntPtr range);
    private static void PrintRichText(RichTextBox text, Bitmap image)
    {
        using (var graphics = Graphics.FromImage(image)) {
            graphics.Clear(Color.White);
            var area = new NativeRectangle { Right = (int)(image.Width * 1440F / graphics.DpiX), Bottom = (int)(image.Height * 1440F / graphics.DpiY) };
            IntPtr device = graphics.GetHdc();
            try {
                var range = new FormatRange { Device = device, Target = device, Area = area, Page = area,
                    Text = new CharacterRange { First = 0, Last = -1 } };
                PrintMessage(text.Handle, 0x0439, new IntPtr(1), ref range);
                ClearPrintCache(text.Handle, 0x0439, IntPtr.Zero, IntPtr.Zero);
            }
            finally { graphics.ReleaseHdc(device); }
        }
    }
}
