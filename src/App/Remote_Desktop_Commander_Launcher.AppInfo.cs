using System;

internal static class AppInfo
{
    internal const string Title = "Commander Companion";
    internal const string Repository = "ZeroFlagLabs/commander-companion";
    internal const string ProjectUrl = "https://github.com/" + Repository;
    internal const string ReleasesUrl = ProjectUrl + "/releases/latest";
    internal const string ReleaseApiUrl = "https://api.github.com/repos/" + Repository + "/releases/latest";
    internal static Version Version { get { return typeof(AppInfo).Assembly.GetName().Version; } }
    internal static string VersionText { get { return Version.ToString(3); } }
}
