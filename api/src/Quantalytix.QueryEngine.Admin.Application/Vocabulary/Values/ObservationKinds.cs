namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>observation_kind</c> vocabulary: what one fact row of a time binding represents.</summary>
public static class ObservationKinds
{
    /// <summary>The row occurs at an instant or event time.</summary>
    public const string Instant = "instant";

    /// <summary>The row describes state observed at a point in time.</summary>
    public const string Snapshot = "snapshot";

    /// <summary>The row summarizes or represents an interval.</summary>
    public const string Interval = "interval";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "observation_kind",
        Description = "What one fact row of a time binding represents.",
        Values =
        [
            new() { Value = Instant, Description = "The row occurs at an instant or event time." },
            new() { Value = Snapshot, Description = "The row describes state observed at a point in time." },
            new() { Value = Interval, Description = "The row summarizes or represents an interval." }
        ]
    };
}
