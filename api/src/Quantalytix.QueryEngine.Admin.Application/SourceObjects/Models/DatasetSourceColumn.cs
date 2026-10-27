namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>The contract's <c>DatasetSourceColumn</c>: a column of a source object with the field type it reads as.</summary>
public sealed record DatasetSourceColumn
{
    /// <summary>The column's name.</summary>
    public required string Name { get; init; }

    /// <summary>The StarRocks type as <c>information_schema</c> writes it, such as <c>decimal(20,2)</c>.</summary>
    public required string NativeType { get; init; }

    /// <summary>The <c>data_type</c> a field over the column gets; null when fields cannot read it.</summary>
    public required string? MappedType { get; init; }

    /// <summary>The decimal precision a field over the column gets; null unless decimal.</summary>
    public required int? NumericPrecision { get; init; }

    /// <summary>The decimal scale a field over the column gets; null unless decimal.</summary>
    public required int? NumericScale { get; init; }

    /// <summary>Whether the column accepts null.</summary>
    public required bool IsNullable { get; init; }

    /// <summary>Whether the API added the column, which it does only to the managed tables it creates (OI-33).</summary>
    public required bool IsGenerated { get; init; }
}
