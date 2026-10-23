namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_report_line</c> row: an overlay row of a report, placed among the rows of the report dimension's members.</summary>
public sealed record ReportLineRecord
{
    /// <summary>Primary key.</summary>
    public required long ReportLineId { get; init; }

    /// <summary>The owning report.</summary>
    public required long ReportId { get; init; }

    /// <summary>Key, unique among the report's overlay rows and distinct from every member key of the report dimension.</summary>
    public required string LineKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>report_line_kind</c> value.</summary>
    public required string LineKind { get; init; }

    /// <summary>The row it sits under: a member key or another overlay row's key; null at the top level.</summary>
    public string? ParentKey { get; init; }

    /// <summary>The key of the model's calculated measure a <c>formula</c> row evaluates.</summary>
    public string? CalculatedMeasureKey { get; init; }

    /// <summary>The column a <c>formula</c> row places its value in.</summary>
    public long? ValueReportColumnId { get; init; }

    /// <summary>Order among the row's siblings, members and overlay rows alike.</summary>
    public required int SortOrder { get; init; }

    /// <summary>Whether the row shows; a hidden row's children take its place.</summary>
    public required bool IsVisible { get; init; }

    /// <summary>Optional presentation payload passed through to clients.</summary>
    public string? PayloadJson { get; init; }

    /// <summary>Reserved: line filters are not yet supported.</summary>
    public string? FilterJson { get; init; }

    /// <summary>Optional presentation settings passed through to clients.</summary>
    public string? SettingsJson { get; init; }

    /// <summary>The key of the report style the row shows; null for its role's default style.</summary>
    public string? StyleKey { get; init; }
}
