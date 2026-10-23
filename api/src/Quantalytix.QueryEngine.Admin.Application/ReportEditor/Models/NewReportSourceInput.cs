namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// A new report source, as <c>POST reports/{reportId}/sources</c> takes it: its fixed key, dataset and mapping method, with the
/// role a bridge source starts from or the field a direct source reads row keys from, and its editable attributes.
/// </summary>
public sealed record NewReportSourceInput : ReportSourceInput
{
    /// <summary>Key, unique among the report's sources and fixed once created; a bridge source's bridge takes it in its key.</summary>
    public required string SourceKey { get; init; }

    /// <summary>The dataset, which the active release pins; the release a run reads decides its version.</summary>
    public required long DatasetId { get; init; }

    /// <summary>A <c>report_mapping_method</c> value: <c>bridge</c> or <c>direct</c>.</summary>
    public required string MappingMethod { get; init; }

    /// <summary>
    /// For a bridge source, a role of an analytical dimension that the dataset binds into members: the source's items are that
    /// dimension's members. Null for a direct source.
    /// </summary>
    public long? DimensionRoleId { get; init; }

    /// <summary>For a direct source, the enabled dataset field whose values are row keys. Null for a bridge source.</summary>
    public string? FieldKey { get; init; }

    /// <summary>
    /// For a bridge source, another bridge source of the report with the same item dimension whose entries the new bridge starts
    /// with, as a copy that is never linked; null to start without entries.
    /// </summary>
    public long? CopyFromSourceId { get; init; }
}
