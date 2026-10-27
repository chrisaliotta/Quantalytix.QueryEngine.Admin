namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>observation_selection</c> vocabulary: which observations of each series a time-aware measure uses.</summary>
public static class ObservationSelections
{
    /// <summary>Use every eligible observation in the interval; typical for flow sums and period averages.</summary>
    public const string All = "all";

    /// <summary>Select the earliest observation inside each requested period for each series.</summary>
    public const string FirstInPeriod = "first_in_period";

    /// <summary>Select the latest observation inside each requested period for each series.</summary>
    public const string LastInPeriod = "last_in_period";

    /// <summary>Resolve state at the period's inclusive start boundary using the boundary match and lookback rules.</summary>
    public const string AtStart = "at_start";

    /// <summary>Resolve state at the period's exclusive end boundary using the boundary match rules.</summary>
    public const string AtEnd = "at_end";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "observation_selection",
        Key = "observationSelection",
        Description = "Which observations of each series a time-aware measure uses.",
        Values =
        [
            new() { Value = All, Label = "All", Description = "Use every eligible observation in the interval; typical for flow sums and period averages." },
            new() { Value = FirstInPeriod, Label = "First in period", Description = "Select the earliest observation inside each requested period for each series." },
            new() { Value = LastInPeriod, Label = "Last in period", Description = "Select the latest observation inside each requested period for each series." },
            new() { Value = AtStart, Label = "At start", Description = "Resolve state at the period's inclusive start boundary using the boundary match and lookback rules." },
            new() { Value = AtEnd, Label = "At end", Description = "Resolve state at the period's exclusive end boundary using the boundary match rules." }
        ]
    };
}
