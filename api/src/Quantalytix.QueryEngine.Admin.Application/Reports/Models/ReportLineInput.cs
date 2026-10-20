namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>One overlay row of a report version's complete overlay row list.</summary>
public sealed record ReportLineInput
{
    /// <summary>The row's ID when it already exists; null for a new row.</summary>
    public long? ReportLineId { get; init; }

    /// <summary>Key, unique among the version's overlay rows and distinct from every member key of the report's dimension.</summary>
    public required string LineKey { get; init; }

    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>report_line_kind</c> value.</summary>
    public required string LineKind { get; init; }

    /// <summary>The row it sits under: a member key of the report's dimension or the key of an overlay row earlier in the list; null at the top level.</summary>
    public string? ParentKey { get; init; }

    /// <summary>The calculated measure key of the report's model version that a <c>formula</c> row evaluates.</summary>
    public string? CalculatedMeasureKey { get; init; }

    /// <summary>The key of the column a <c>formula</c> row places its value in.</summary>
    public string? ValueColumnKey { get; init; }

    /// <summary>Order among the row's siblings, compared with the members' sort orders; a member comes first on a tie.</summary>
    public required int SortOrder { get; init; }

    /// <summary>Whether the row shows; a hidden row's children take its place.</summary>
    public required bool IsVisible { get; init; }

    /// <summary>Optional presentation payload passed through to clients.</summary>
    public string? PayloadJson { get; init; }

    /// <summary>Optional presentation settings passed through to clients.</summary>
    public string? SettingsJson { get; init; }
}
