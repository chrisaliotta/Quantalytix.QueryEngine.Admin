using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// A report's content with its model's metrics, which the report rules evaluate in memory: the report as it stands, or as a write to
/// the report or its model would leave it.
/// </summary>
public sealed record ReportGraph
{
    /// <summary>The report, with its settings.</summary>
    public required ReportRecord Report { get; init; }

    /// <summary>The members of the report dimension's version, which are the report's member rows.</summary>
    public required IReadOnlyList<DimensionMemberRecord> Members { get; init; }

    /// <summary>The report's overlay rows.</summary>
    public required IReadOnlyList<ReportLineRecord> Lines { get; init; }

    /// <summary>The report's member options.</summary>
    public required IReadOnlyList<ReportMemberOptionRecord> MemberOptions { get; init; }

    /// <summary>The report's variable scopes.</summary>
    public required IReadOnlyList<ReportVariableScopeRecord> Scopes { get; init; }

    /// <summary>The report's columns.</summary>
    public required IReadOnlyList<ReportColumnRecord> Columns { get; init; }

    /// <summary>The report's source profiles.</summary>
    public required IReadOnlyList<SourceProfileRecord> Profiles { get; init; }

    /// <summary>The picks of the report's profiles.</summary>
    public required IReadOnlyList<SourceProfileVariableRecord> Picks { get; init; }

    /// <summary>The report's sources.</summary>
    public required IReadOnlyList<ReportSourceRecord> Sources { get; init; }

    /// <summary>The variables the report's sources supply.</summary>
    public required IReadOnlyList<ReportSourceVariableRecord> SourceVariables { get; init; }

    /// <summary>The rules of the report's bridge sources: the entries of their bridges.</summary>
    public required IReadOnlyList<ReportSourceItem> SourceItems { get; init; }

    /// <summary>The report's calculation dimensions, enabled or not.</summary>
    public required IReadOnlyList<CalculationDimensionRecord> Dimensions { get; init; }

    /// <summary>The model's variables.</summary>
    public required IReadOnlyList<VariableRecord> Variables { get; init; }

    /// <summary>The model's calculated measures.</summary>
    public required IReadOnlyList<CalculatedMeasureRecord> CalculatedMeasures { get; init; }

    /// <summary>The <c>report_key</c> of the report.</summary>
    public string ReportKey => Report.ReportKey;

    /// <summary>The calculation dimensions the engine reads: the enabled ones.</summary>
    public IEnumerable<CalculationDimensionRecord> EnabledDimensions => Dimensions.Where(dimension => dimension.IsEnabled);
}
