namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>A column of a materialized view holding one JSON path's value, typed like the field that reads it.</summary>
public sealed record MaterializedViewColumn
{
    /// <summary>The view column's name.</summary>
    public required string Name { get; init; }

    /// <summary>The JSON column of the source object.</summary>
    public required string SourceColumn { get; init; }

    /// <summary>The JSON path, already checked against <c>DatasetPatterns.JsonPath</c>, so it can be inlined.</summary>
    public required string JsonPath { get; init; }

    /// <summary>A <c>data_type</c> value.</summary>
    public required string DataType { get; init; }

    /// <summary>The decimal precision; null unless decimal.</summary>
    public required int? NumericPrecision { get; init; }

    /// <summary>The decimal scale; null unless decimal.</summary>
    public required int? NumericScale { get; init; }
}
