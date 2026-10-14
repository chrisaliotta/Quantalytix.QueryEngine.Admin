namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>The logical type of a field: a <c>data_type</c> value, with precision and scale for decimals.</summary>
public sealed record FieldType
{
    /// <summary>A <c>data_type</c> value.</summary>
    public required string DataType { get; init; }

    /// <summary>The decimal precision; null for every other data type.</summary>
    public int? NumericPrecision { get; init; }

    /// <summary>The decimal scale; null for every other data type.</summary>
    public int? NumericScale { get; init; }
}
