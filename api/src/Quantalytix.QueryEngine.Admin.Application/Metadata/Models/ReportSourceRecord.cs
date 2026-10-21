namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_report_source</c> row: one dataset a report reads, and how its rows classify into the report's rows. A direct source's
/// field holds row keys; a bridge source reads the dataset's binding of a role, whose members its own bridge maps to rows. Dataset
/// content is named by key in the dataset version the active release pins.
/// </summary>
public sealed record ReportSourceRecord
{
    /// <summary>Primary key.</summary>
    public required long ReportSourceId { get; init; }

    /// <summary>The owning report.</summary>
    public required long ReportId { get; init; }

    /// <summary>Key, unique among the report's sources; fixed at creation.</summary>
    public required string SourceKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The dataset; the active release decides its version. Fixed at creation.</summary>
    public required long DatasetId { get; init; }

    /// <summary>A <c>report_mapping_method</c> value; fixed at creation.</summary>
    public required string MappingMethod { get; init; }

    /// <summary>The dataset field whose values are row keys, for a direct source.</summary>
    public string? FieldKey { get; init; }

    /// <summary>The role whose dataset binding a bridge source starts from.</summary>
    public long? RoleId { get; init; }

    /// <summary>The source's own bridge, from the role's dimension into the report's dimension, for a bridge source.</summary>
    public long? BridgeId { get; init; }

    /// <summary>The time field the source's periods are read on.</summary>
    public required string TimeFieldKey { get; init; }
}
