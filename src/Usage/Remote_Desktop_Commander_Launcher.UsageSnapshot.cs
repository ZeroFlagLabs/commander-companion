using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

internal enum UsageState { Loading, Free, Pro, SignedOut, Unavailable }

internal sealed class UsageSnapshot
{
    internal UsageState State;
    internal long CallsUsed;
    internal long? CallsIncluded;
    internal DateTimeOffset? ResetsAt;
    internal DateTimeOffset? UpdatedAt;

    internal static UsageSnapshot Status(UsageState state)
    {
        return new UsageSnapshot { State = state };
    }
}

internal static class UsageParser
{
    internal static UsageSnapshot Parse(string json, DateTimeOffset updatedAt)
    {
        var serializer = new JavaScriptSerializer { MaxJsonLength = 32768, RecursionLimit = 12 };
        var root = serializer.DeserializeObject(json) as Dictionary<string, object>;
        object value;
        var usage = root != null && root.TryGetValue("usage", out value)
            ? value as Dictionary<string, object> : null;
        if (usage == null) throw new FormatException("Usage details are missing.");
        string plan = Required(usage, "plan") as string;
        long used = Count(Required(usage, "callsUsed"));
        object included = Required(usage, "callsIncluded");
        DateTimeOffset? resets = null;
        if (usage.TryGetValue("resetsAt", out value) && value != null)
        {
            DateTimeOffset parsed;
            if (!(value is string) || !DateTimeOffset.TryParse((string)value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out parsed)) throw new FormatException("Invalid reset date.");
            resets = parsed;
        }
        if (plan == "pro" && included == null)
            return new UsageSnapshot { State = UsageState.Pro, CallsUsed = used, UpdatedAt = updatedAt, ResetsAt = resets };
        if (plan == "free" && included != null && resets.HasValue)
        {
            long allowance = Count(included);
            if (allowance < 1) throw new FormatException("Invalid monthly allowance.");
            return new UsageSnapshot { State = UsageState.Free, CallsUsed = used, CallsIncluded = allowance,
                ResetsAt = resets, UpdatedAt = updatedAt };
        }
        // A missing limit is not sufficient evidence that an unknown plan is unlimited.
        throw new FormatException("The account plan could not be verified.");
    }

    private static object Required(Dictionary<string, object> source, string name)
    {
        object value;
        if (!source.TryGetValue(name, out value)) throw new FormatException("Required usage detail is missing.");
        return value;
    }

    private static long Count(object value)
    {
        if (!(value is int) && !(value is long) && !(value is decimal))
            throw new FormatException("Invalid tool-call count.");
        decimal number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        if (number < 0 || number > long.MaxValue || decimal.Truncate(number) != number)
            throw new FormatException("Invalid tool-call count.");
        return (long)number;
    }
}

internal static class UsageOrigin
{
    internal const string Page = "https://mcp.desktopcommander.app/usage";
    internal const string Endpoint = "https://auth.desktopcommander.app/auth/billing/usage";

    internal static bool IsDashboard(string source)
    {
        Uri uri;
        return Uri.TryCreate(source, UriKind.Absolute, out uri) && uri.Scheme == "https"
            && string.Equals(uri.Host, "mcp.desktopcommander.app", StringComparison.OrdinalIgnoreCase)
            && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo);
    }

    internal static bool IsSecurePage(string source)
    {
        Uri uri;
        return Uri.TryCreate(source, UriKind.Absolute, out uri) && uri.Scheme == "https"
            && string.IsNullOrEmpty(uri.UserInfo);
    }

    internal static bool IsExpectedMessage(string source, string expectedId, string receivedId)
    {
        return IsDashboard(source) && !string.IsNullOrEmpty(expectedId) && expectedId.Length == 32
            && string.Equals(expectedId, receivedId, StringComparison.Ordinal);
    }
}
