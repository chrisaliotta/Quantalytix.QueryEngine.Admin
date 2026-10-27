namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>data_type</c> vocabulary: the logical data type of a field.</summary>
public static class DataTypes
{
    /// <summary>Logical true/false value.</summary>
    public const string Boolean = "boolean";

    /// <summary>Signed 32-bit integer.</summary>
    public const string Int32 = "int32";

    /// <summary>Signed 64-bit integer.</summary>
    public const string Int64 = "int64";

    /// <summary>Fixed-precision decimal; precision and scale are required where applicable.</summary>
    public const string Decimal = "decimal";

    /// <summary>Character string value.</summary>
    public const string String = "string";

    /// <summary>Calendar date without a time of day or time zone.</summary>
    public const string Date = "date";

    /// <summary>Date and time without a request-specific time zone conversion.</summary>
    public const string Datetime = "datetime";

    /// <summary>Instant interpreted with the declared source time zone semantics.</summary>
    public const string Timestamp = "timestamp";

    /// <summary>JSON value exposed as a logical field.</summary>
    public const string Json = "json";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "data_type",
        Key = "dataType",
        Description = "Logical data type of a field.",
        Values =
        [
            new() { Value = Boolean, Label = "Boolean", Description = "Logical true/false value." },
            new() { Value = Int32, Label = "Int32", Description = "Signed 32-bit integer." },
            new() { Value = Int64, Label = "Int64", Description = "Signed 64-bit integer." },
            new() { Value = Decimal, Label = "Decimal", Description = "Fixed-precision decimal; precision and scale are required where applicable." },
            new() { Value = String, Label = "String", Description = "Character string value." },
            new() { Value = Date, Label = "Date", Description = "Calendar date without a time of day or time zone." },
            new() { Value = Datetime, Label = "Datetime", Description = "Date and time without a request-specific time zone conversion." },
            new() { Value = Timestamp, Label = "Timestamp", Description = "Instant interpreted with the declared source time zone semantics." },
            new() { Value = Json, Label = "Json", Description = "JSON value exposed as a logical field." }
        ]
    };
}
