using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
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
                        if (state != "SignedOut") throw new Exception("Fresh account profile returned " + state + ".");
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
                new[] { updates }, CultureInfo.InvariantCulture)) Save(about, Path.Combine(output, "about.png"));
            using (var licences = (Form)Activator.CreateInstance(app.GetType("LicenceWindow"), true)) {
                var text = (TextBox)licences.Controls[0];
                if (!text.Text.Contains("Microsoft Corporation") || !text.Text.Contains("MIT License"))
                    throw new Exception("Embedded licence notices are incomplete.");
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
                control.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                graphics.DrawImageUnscaled(image, point);
            }
            DrawChildren(control, graphics, point);
        }
    }
}
