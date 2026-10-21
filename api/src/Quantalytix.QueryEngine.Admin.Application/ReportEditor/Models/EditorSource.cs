using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// One source of a report: the dataset it reads, how its rows reach the report's rows, and the dataset measures that supply the
/// model's variables. A bridge source's rows classify into members of its item dimension, which its bridge maps to rows; a direct
/// source's field holds row keys.
/// </summary>
public sealed record EditorSource
{
    /// <summary>The source.</summary>
    public required ReportSourceRecord Source { get; init; }

    /// <summary>The <c>dataset_key</c> of the source's dataset.</summary>
    public required string DatasetKey { get; init; }

    /// <summary>The <c>role_key</c> of a bridge source's role; null for a direct source.</summary>
    public string? RoleKey { get; init; }

    /// <summary>The dimension of a bridge source's role, whose members are its items; null for a direct source.</summary>
    public long? ItemDimensionId { get; init; }

    /// <summary>The published version of the item dimension, whose members the item pickers list; null for a direct source.</summary>
    public long? ItemDimensionVersionId { get; init; }

    /// <summary>The one version of a bridge source's bridge, which holds its entries; null for a direct source.</summary>
    public long? BridgeVersionId { get; init; }

    /// <summary>How many assignable items of a bridge source map to no row; null for a direct source.</summary>
    public int? UnmappedCount { get; init; }

    /// <summary>The variables the source supplies, by variable key.</summary>
    public required IReadOnlyList<ReportSourceVariableRecord> Variables { get; init; }
}
