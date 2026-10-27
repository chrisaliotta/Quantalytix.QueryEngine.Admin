namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>
/// The <c>time_zone</c> vocabulary: the IANA time zones offered for time bindings' source and query time zones. The columns are
/// free text; these are the offered values. Served by the tenant module until the datasets module declares it.
/// </summary>
public static class TimeZones
{
    /// <summary>The vocabulary's definition, as the web client reads it.</summary>
    public static readonly Vocabulary.Vocabulary Definition = new()
    {
        Name = "time_zone",
        Key = "timeZone",
        Description = "IANA time zones offered for source and query time zones.",
        Values =
        [
            new() { Value = "UTC", Label = "UTC", Description = "Coordinated Universal Time." },
            new() { Value = "America/New_York", Label = "America/New_York", Description = "US Eastern time." },
            new() { Value = "America/Chicago", Label = "America/Chicago", Description = "US Central time." },
            new() { Value = "America/Los_Angeles", Label = "America/Los_Angeles", Description = "US Pacific time." },
            new() { Value = "Europe/London", Label = "Europe/London", Description = "UK time." },
            new() { Value = "Europe/Frankfurt", Label = "Europe/Frankfurt", Description = "Central European time." },
            new() { Value = "Asia/Singapore", Label = "Asia/Singapore", Description = "Singapore time." },
            new() { Value = "Asia/Tokyo", Label = "Asia/Tokyo", Description = "Japan time." }
        ]
    };
}
