namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetField</c>: a field with its id and materialization.</summary>
public sealed record DatasetField : DatasetFieldDefinition
{
    /// <summary>The field's id, stored or temporary.</summary>
    public required string Id { get; init; }

    /// <summary>The field's column in its source object's materialized view (OI-32); null when not materialized.</summary>
    public required DatasetFieldMaterialization? Materialization { get; init; }
}
