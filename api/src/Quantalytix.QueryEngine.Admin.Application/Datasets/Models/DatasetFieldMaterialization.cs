namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The contract's <c>DatasetFieldMaterialization</c>: the field's column in its source object's materialized view, with the view's
/// refresh status as StarRocks reports it (OI-32).
/// </summary>
public sealed record DatasetFieldMaterialization
{
    /// <summary>The materialized view.</summary>
    public required string ViewName { get; init; }

    /// <summary>The field's column in the view (<c>qe_field.materialized_column</c>).</summary>
    public required string ColumnName { get; init; }

    /// <summary>A <see cref="MaterializationStates"/> value.</summary>
    public required string State { get; init; }

    /// <summary>When the view was last defined, which every change of its columns does (UTC).</summary>
    public required DateTime RequestedAt { get; init; }

    /// <summary>When the view's last refresh finished (UTC); null before the first.</summary>
    public required DateTime? RefreshedAt { get; init; }

    /// <summary>Why the last refresh failed; null otherwise.</summary>
    public required string? Error { get; init; }
}
