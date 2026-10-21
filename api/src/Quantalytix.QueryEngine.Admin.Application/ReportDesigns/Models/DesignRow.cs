namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>One row of a report design: a member of the report dimension or an overlay row of the report version.</summary>
public sealed record DesignRow
{
    /// <summary>The member or overlay row key, which requests and the row mappings name it by.</summary>
    public required string Key { get; init; }

    /// <summary>The label the report shows.</summary>
    public required string Label { get; init; }

    /// <summary>A <c>design_row_kind</c> value.</summary>
    public required string Kind { get; init; }

    /// <summary>The key of the row it sits under; null for the top level.</summary>
    public string? ParentKey { get; init; }

    /// <summary>Order among its siblings; a member comes before an overlay row on a tie.</summary>
    public required int SortOrder { get; init; }

    /// <summary>Whether the row shows; a hidden row's children take its place.</summary>
    public required bool IsVisible { get; init; }

    /// <summary>Whether a group row shows its total; false makes it a header. Always true for the other kinds.</summary>
    public required bool ShowsValues { get; init; }

    /// <summary>The calculated measure a formula row evaluates; null for the other kinds.</summary>
    public string? CalculatedMeasureKey { get; init; }

    /// <summary>The key of the column a formula row places its value in; null for the other kinds.</summary>
    public string? ValueColumnKey { get; init; }

    /// <summary>The items of the design's sources that map to the row, by source, then item key.</summary>
    public required IReadOnlyList<DesignRowMapping> Mappings { get; init; }

    /// <summary>The source codes that mapping sets into the report dimension map to the row, which the design shows but does not edit.</summary>
    public required IReadOnlyList<DesignMappedCode> MappedCodes { get; init; }
}
