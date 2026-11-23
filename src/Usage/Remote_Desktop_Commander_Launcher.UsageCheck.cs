using System;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// Explicit diagnostics only; normal startup never writes account usage to disk.
internal static class UsageCheck
{
    internal static int Run(string report)
    {
        int exit = 2;
        using (var context = new ApplicationContext())
        using (var timer = new Timer { Interval = 100 })
        {
            timer.Tick += async delegate {
                timer.Stop();
                try
                {
                    using (var account = new UsageAccountWindow())
                    {
                        var snapshot = await account.RefreshUsageAsync();
                        var output = new {
                            state = snapshot.State.ToString(), callsUsed = snapshot.UpdatedAt.HasValue ? (long?)snapshot.CallsUsed : null,
                            callsIncluded = snapshot.CallsIncluded,
                            diagnostic = account.Diagnostic,
                            resetsAt = snapshot.ResetsAt.HasValue ? snapshot.ResetsAt.Value.ToString("O", CultureInfo.InvariantCulture) : null
                        };
                        File.WriteAllText(report, new JavaScriptSerializer().Serialize(output));
                        exit = snapshot.State == UsageState.Unavailable ? 2 : 0;
                    }
                }
                catch { exit = 9; }
                finally { context.ExitThread(); }
            };
            timer.Start();
            Application.Run(context);
        }
        return exit;
    }
}
