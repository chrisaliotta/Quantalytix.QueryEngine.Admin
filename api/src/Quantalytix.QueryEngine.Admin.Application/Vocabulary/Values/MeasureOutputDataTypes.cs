namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>measure_output_data_type</c> vocabulary: every <see cref="DataTypes"/> value except <c>json</c>.</summary>
public static class MeasureOutputDataTypes
{
    /// <summary>Logical true/false value.</summary>
    public const string Boolean = DataTypes.Boolean;

    /// <summary>Signed 32-bit integer.</summary>
    public const string Int32 = DataTypes.Int32;

    /// <summary>Signed 64-bit integer.</summary>
    public const string Int64 = DataTypes.Int64;

    /// <summary>Fixed-precision decimal; precision and scale are required where applicable.</summary>
    public const string Decimal = DataTypes.Decimal;

    /// <summary>Character string value.</summary>
    public const string String = DataTypes.String;

    /// <summary>Calendar date without a time of day or time zone.</summary>
    public const string Date = DataTypes.Date;

    /// <summary>Date and time without a request-specific time zone conversion.</summary>
    public const string Datetime = DataTypes.Datetime;

    /// <summary>Instant interpreted with the declared source time zone semantics.</summary>
    public const string Timestamp = DataTypes.Timestamp;

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "measure_output_data_type",
        Description = "Logical data type of a measure's output: every data type except json.",
        Values = [.. DataTypes.Definition.Values.Where(value => value.Value != DataTypes.Json)]
    };
}
