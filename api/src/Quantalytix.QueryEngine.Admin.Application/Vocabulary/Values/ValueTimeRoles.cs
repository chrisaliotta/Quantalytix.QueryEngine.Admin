namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>value_time_role</c> vocabulary: the point in time a measure value belongs to.</summary>
public static class ValueTimeRoles
{
    /// <summary>The value describes state at the observation timestamp.</summary>
    public const string Point = "point";

    /// <summary>The value belongs to the interval's opening boundary.</summary>
    public const string IntervalOpen = "interval_open";

    /// <summary>The value belongs to the interval's closing boundary.</summary>
    public const string IntervalClose = "interval_close";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "value_time_role",
        Key = "valueTimeRole",
        Description = "The point in time a measure value belongs to.",
        Values =
        [
            new() { Value = Point, Label = "Point", Description = "The value describes state at the observation timestamp." },
            new() { Value = IntervalOpen, Label = "Interval open", Description = "The value belongs to the interval's opening boundary." },
            new() { Value = IntervalClose, Label = "Interval close", Description = "The value belongs to the interval's closing boundary." }
        ]
    };
}
