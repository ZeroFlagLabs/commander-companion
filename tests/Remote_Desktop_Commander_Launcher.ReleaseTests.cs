using System;
using System.Collections.Generic;

internal static class ReleaseTests
{
    internal static void Run(List<string> report)
    {
        var current = new Version(1, 0, 0, 0);
        foreach (string tag in new[] { "v1.0.0", "1.0.0.0", "v0.9.9" })
            Require(!ReleaseInfo.Parse(Json(tag), current).UpdateAvailable, "Current or older release offered as an update.");
        foreach (string tag in new[] { "v1.0.1", "v1.10.0", "v2.0.0", "v1.0.0.1" }) {
            var result = ReleaseInfo.Parse(Json(tag), current);
            Require(result.UpdateAvailable && result.Url == AppInfo.ReleasesUrl, "New version or fixed project link was lost.");
        }
        Require(!ReleaseInfo.Parse(Json("v4.0.0").Replace("\"draft\":false", "\"draft\":true"), current).UpdateAvailable,
            "Draft release offered as an update.");
        Require(!ReleaseInfo.Parse(Json("v4.0.0").Replace("\"prerelease\":false", "\"prerelease\":true"), current).UpdateAvailable,
            "Prerelease offered as a stable update.");
        foreach (string json in new[] { "{}", "null", "[]", "{\"tag_name\":\"v4.0.0\"}",
            Json("v4.0.0-beta"), Json("v4.0"), Json("v4.0.0\n"),
            Json("v99999999999999.0.0"), Json("v4.0.0").Replace("\"draft\":false", "\"draft\":\"false\"") }) {
            bool rejected = false;
            try { ReleaseInfo.Parse(json, current); }
            catch (ArgumentException) { rejected = true; }
            catch (FormatException) { rejected = true; }
            Require(rejected, "Invalid release information was accepted.");
        }
        report.Add("PASS: update checks compare numeric versions, ignore drafts/prereleases, reject incomplete metadata and link only to this project's GitHub releases.");
    }

    private static string Json(string tag)
    {
        return "{\"draft\":false,\"prerelease\":false,\"tag_name\":\"" + tag.Replace("\n", "\\n") +
            "\",\"html_url\":\"https://example.invalid/untrusted\"}";
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
