namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>time_value_type</c> vocabulary: the type of a time binding's source value.</summary>
public static class TimeValueTypes
{
    /// <summary>Calendar date values.</summary>
    public const string Date = "date";

    /// <summary>Instant values interpreted in the source time zone.</summary>
    public const string Timestamp = "timestamp";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "time_value_type",
        Key = "timeValueType",
        Description = "Type of a time binding's source value.",
        Values =
        [
            new() { Value = Date, Label = "Date", Description = "Calendar date values." },
            new() { Value = Timestamp, Label = "Timestamp", Description = "Instant values interpreted in the source time zone." }
        ]
    };
}
