namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetNewColumnInput</c>: a physical column the API creates, typed like a field.</summary>
public sealed record DatasetNewColumnInput
{
    /// <summary>The column's name.</summary>
    public required string Name { get; init; }

    /// <summary>A <c>data_type</c> value.</summary>
    public required string DataType { get; init; }

    /// <summary>The decimal precision; null unless decimal.</summary>
    public required int? NumericPrecision { get; init; }

    /// <summary>The decimal scale; null unless decimal.</summary>
    public required int? NumericScale { get; init; }

    /// <summary>Whether the column accepts null.</summary>
    public required bool IsNullable { get; init; }
}
