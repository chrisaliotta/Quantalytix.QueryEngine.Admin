using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// A report version as one design: its rows with the source items mapped to each, and its sources, each a bridge from a bound
/// dimension with the dataset measures that supply the model's variables.
/// </summary>
public sealed record ReportDesign
{
    /// <summary>The report.</summary>
    public required long ReportId { get; init; }

    /// <summary>The report version.</summary>
    public required long ReportVersionId { get; init; }

    /// <summary>The status the report version and the versions of the report's own objects share.</summary>
    public required string Status { get; init; }

    /// <summary>The report dimension's version whose members are the member rows.</summary>
    public required long DimensionVersionId { get; init; }

    /// <summary>Every row: members of the report dimension and the version's overlay rows, by sort order, then key.</summary>
    public required IReadOnlyList<DesignRow> Rows { get; init; }

    /// <summary>The sources that classify through the report's own bridges, by key.</summary>
    public required IReadOnlyList<DesignSource> Sources { get; init; }

    /// <summary>The variable sources that classify directly or through a mapping set, which the design shows but does not edit, by key.</summary>
    public required IReadOnlyList<VariableSourceRecord> OtherSources { get; init; }
}
