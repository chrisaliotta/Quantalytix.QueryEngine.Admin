namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>periodicity</c> vocabulary: a calendar grouping period.</summary>
public static class Periodicities
{
    /// <summary>Calendar-day grouping.</summary>
    public const string Day = "day";

    /// <summary>Calendar-month grouping.</summary>
    public const string Month = "month";

    /// <summary>Calendar-quarter grouping.</summary>
    public const string Quarter = "quarter";

    /// <summary>Calendar-year grouping.</summary>
    public const string Year = "year";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "periodicity",
        Description = "Calendar grouping period.",
        Values =
        [
            new() { Value = Day, Description = "Calendar-day grouping." },
            new() { Value = Month, Description = "Calendar-month grouping." },
            new() { Value = Quarter, Description = "Calendar-quarter grouping." },
            new() { Value = Year, Description = "Calendar-year grouping." }
        ]
    };
}
