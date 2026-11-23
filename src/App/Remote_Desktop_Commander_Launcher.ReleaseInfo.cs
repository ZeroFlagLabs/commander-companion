using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

internal sealed class ReleaseInfo
{
    internal string Status;
    internal bool UpdateAvailable;
    internal string Url;

    internal static ReleaseInfo Message(string text) { return new ReleaseInfo { Status = text }; }

    internal static ReleaseInfo Parse(string json, Version current)
    {
        var root = new JavaScriptSerializer { MaxJsonLength = 1048576, RecursionLimit = 32 }
            .DeserializeObject(json) as Dictionary<string, object>;
        object draft, prerelease, tag;
        if (root == null || !root.TryGetValue("draft", out draft) || !(draft is bool) ||
            !root.TryGetValue("prerelease", out prerelease) || !(prerelease is bool))
            throw new FormatException("Release information is incomplete.");
        if ((bool)draft || (bool)prerelease) return Message("No newer stable release found.");
        if (!root.TryGetValue("tag_name", out tag) || !(tag is string))
            throw new FormatException("Release version is missing.");
        Version version;
        if (!TryVersion((string)tag, out version)) throw new FormatException("Release version is invalid.");
        if (version <= Normalise(current)) return Message("You are up to date.");
        // Use our fixed repository link, never an arbitrary URL supplied in metadata.
        return new ReleaseInfo { Status = (string)tag + " is available. Open GitHub to download.",
            UpdateAvailable = true, Url = AppInfo.ReleasesUrl };
    }

    private static bool TryVersion(string value, out Version version)
    {
        version = null;
        if (!Regex.IsMatch(value, @"\Av?\d+\.\d+\.\d+(?:\.\d+)?\z")) return false;
        Version parsed;
        if (!Version.TryParse(value.TrimStart('v'), out parsed)) return false;
        version = Normalise(parsed);
        return true;
    }

    private static Version Normalise(Version version)
    {
        return new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
    }
}
