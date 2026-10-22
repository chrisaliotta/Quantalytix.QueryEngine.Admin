using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// A report as its editor shows it: the report, its rows with the items each bridge source maps to them, its sources with the
/// variables they supply, and the parameters of its model, whose defaults the rows inherit.
/// </summary>
public sealed record EditorView
{
    /// <summary>The report, with its settings and the model whose variables and calculated measures it names.</summary>
    public required ReportRecord Report { get; init; }

    /// <summary>The report dimension's one version, whose members are the member rows.</summary>
    public required long DimensionVersionId { get; init; }

    /// <summary>Every row: members of the report dimension and the report's overlay rows, by sort order, then key.</summary>
    public required IReadOnlyList<EditorRow> Rows { get; init; }

    /// <summary>The report's sources, by key.</summary>
    public required IReadOnlyList<EditorSource> Sources { get; init; }

    /// <summary>The parameters the report's model declares, by key, which rows set or inherit.</summary>
    public required IReadOnlyList<ModelParameter> Parameters { get; init; }
}
