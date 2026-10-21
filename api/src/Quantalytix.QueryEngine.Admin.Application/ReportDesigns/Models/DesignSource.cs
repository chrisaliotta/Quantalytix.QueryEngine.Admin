using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// One source of a report design: a bridge of the report from the dimension a dataset binds through a role, and the report
/// version's variable sources that classify through it, one per variable the dataset supplies.
/// </summary>
public sealed record DesignSource
{
    /// <summary>The source's bridge.</summary>
    public required long BridgeId { get; init; }

    /// <summary>The source's key: its bridge's key after the report's prefix.</summary>
    public required string Key { get; init; }

    /// <summary>The source's label: its bridge's name.</summary>
    public required string Label { get; init; }

    /// <summary>The bound dimension whose members the source maps to rows: its bridge's source dimension.</summary>
    public required long SourceDimensionId { get; init; }

    /// <summary>
    /// Whether every variable source reads the same dataset, role, time field and filter, so the design can edit them as one;
    /// false for a bridge the design did not create that several datasets share. Its row mappings stay editable.
    /// </summary>
    public required bool IsUniform { get; init; }

    /// <summary>The role the dataset binds the bound dimension through; null unless uniform.</summary>
    public long? RoleId { get; init; }

    /// <summary>The dataset; null unless uniform.</summary>
    public long? DatasetId { get; init; }

    /// <summary>The dataset's time field; null unless uniform.</summary>
    public string? TimeFieldKey { get; init; }

    /// <summary>The structured filter over the dataset's fields; null when there is none or the source is not uniform.</summary>
    public string? SourceFilterJson { get; init; }

    /// <summary>The variable sources that classify through the bridge, by variable key.</summary>
    public required IReadOnlyList<VariableSourceRecord> Variables { get; init; }
}
