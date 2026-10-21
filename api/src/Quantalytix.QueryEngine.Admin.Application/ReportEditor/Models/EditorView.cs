using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// A report as its editor shows it: the report, its rows with the items each bridge source maps to them, and its sources with the
/// variables they supply.
/// </summary>
public sealed record EditorView
{
    /// <summary>The report, with its settings.</summary>
    public required ReportRecord Report { get; init; }

    /// <summary>The published version of the report's model, whose variables and calculated measures the report names.</summary>
    public required long ModelVersionId { get; init; }

    /// <summary>The report dimension's one version, whose members are the member rows.</summary>
    public required long DimensionVersionId { get; init; }

    /// <summary>Every row: members of the report dimension and the report's overlay rows, by sort order, then key.</summary>
    public required IReadOnlyList<EditorRow> Rows { get; init; }

    /// <summary>The report's sources, by key.</summary>
    public required IReadOnlyList<EditorSource> Sources { get; init; }
}
