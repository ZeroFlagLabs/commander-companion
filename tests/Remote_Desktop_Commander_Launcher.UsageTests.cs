using System;
using System.Collections.Generic;

internal static class UsageTests
{
    internal static void Run(List<string> report)
    {
        var now = new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
        string free = "{\"usage\":{\"plan\":\"free\",\"callsUsed\":3340,\"callsIncluded\":10000,\"resetsAt\":\"2026-12-01T00:00:00Z\"}}";
        var parsed = UsageParser.Parse(free, now);
        if (parsed.State != UsageState.Free || parsed.CallsUsed != 3340 || parsed.CallsIncluded != 10000
            || parsed.ResetsAt.Value.Month != 12 || parsed.UpdatedAt != now) throw new Exception("Exact Free usage parsing failed.");
        parsed = UsageParser.Parse("{\"usage\":{\"plan\":\"pro\",\"callsUsed\":12450,\"callsIncluded\":null}}", now);
        if (parsed.State != UsageState.Pro || parsed.CallsIncluded.HasValue || parsed.CallsUsed != 12450)
            throw new Exception("Unlimited Pro parsing failed.");
        parsed = UsageParser.Parse(free.Replace("3340", "10001"), now);
        if (parsed.CallsUsed != 10001) throw new Exception("An exhausted allowance was incorrectly clamped.");
        string[] rejected = {
            "{}", "{\"usage\":null}", free.Replace("3340", "-1"), free.Replace("3340", "3.5"),
            free.Replace("3340", "\"3340\""), free.Replace("10000", "0"), free.Replace("free", "unknown"),
            free.Replace("10000", "null"), free.Replace("2026-12-01T00:00:00Z", "invalid"),
            "{\"usage\":{\"plan\":\"unknown\",\"callsUsed\":4,\"callsIncluded\":null}}",
            "{\"remote_calls_left_pct\":66}", "{\"usage\":{\"plan\":\"pro\",\"callsUsed\":4,\"callsIncluded\":10000}}"
        };
        foreach (string json in rejected)
        {
            bool failed = false;
            try { UsageParser.Parse(json, now); } catch (Exception) { failed = true; }
            if (!failed) throw new Exception("Unverified account data was accepted as exact usage.");
        }
        report.Add("PASS: exact Free counts, reset dates and unlimited Pro are read; missing, rounded and invalid account data are rejected.");
        if (!UsageOrigin.IsDashboard("https://mcp.desktopcommander.app/usage")
            || UsageOrigin.IsDashboard("https://mcp.desktopcommander.app.evil.example/usage")
            || UsageOrigin.IsDashboard("http://mcp.desktopcommander.app/usage")
            || UsageOrigin.IsDashboard("https://mcp.desktopcommander.app:444/usage")
            || UsageOrigin.IsDashboard("https://user@mcp.desktopcommander.app/usage")
            || UsageOrigin.IsDashboard("https://auth.desktopcommander.app/")
            || UsageOrigin.IsSecurePage("file:///C:/secret.txt")) throw new Exception("Account message origin validation failed.");
        report.Add("PASS: account messages require the exact HTTPS dashboard origin; lookalikes, alternate ports and local files are rejected.");
        string nonce = "0123456789abcdef0123456789abcdef";
        if (!UsageOrigin.IsExpectedMessage(UsageOrigin.Page, nonce, nonce)
            || UsageOrigin.IsExpectedMessage(UsageOrigin.Page, null, null)
            || UsageOrigin.IsExpectedMessage(UsageOrigin.Page, nonce, "stale")
            || UsageOrigin.IsExpectedMessage("https://example.com", nonce, nonce))
            throw new Exception("Stale or unsolicited account messages were accepted.");
        report.Add("PASS: stale, unsolicited and foreign account messages cannot replace the displayed usage.");
    }
}
