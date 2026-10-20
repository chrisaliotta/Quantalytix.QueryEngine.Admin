using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// A report version's content with the metrics of the model version it is checked against, which the report rules follow: its
/// model's published version, or the model version being published. The metrics are empty while the model has no published
/// version, which <c>model_version_published</c> reports.
/// </summary>
public sealed record ReportGraph
{
    /// <summary>The report, which names the report's dimension.</summary>
    public required ReportRecord Report { get; init; }

    /// <summary>The report version.</summary>
    public required ReportVersionRecord Version { get; init; }

    /// <summary>The version's columns.</summary>
    public required IReadOnlyList<ReportColumnRecord> Columns { get; init; }

    /// <summary>The version's overlay rows.</summary>
    public required IReadOnlyList<ReportLineRecord> Lines { get; init; }

    /// <summary>The version's member options.</summary>
    public required IReadOnlyList<ReportMemberOptionRecord> MemberOptions { get; init; }

    /// <summary>The version's variable targets.</summary>
    public required IReadOnlyList<ReportVariableTargetRecord> Targets { get; init; }

    /// <summary>The version's source profiles.</summary>
    public required IReadOnlyList<SourceProfileRecord> Profiles { get; init; }

    /// <summary>The picks of the version's profiles.</summary>
    public required IReadOnlyList<SourceProfileVariableRecord> Picks { get; init; }

    /// <summary>The version's enabled sources, which its profiles may pick.</summary>
    public required IReadOnlyList<VariableSourceRecord> Sources { get; init; }

    /// <summary>The version's enabled calculation dimensions.</summary>
    public required IReadOnlyList<CalculationDimensionRecord> Dimensions { get; init; }

    /// <summary>The version's routes.</summary>
    public required IReadOnlyList<CalculationRouteRecord> Routes { get; init; }

    /// <summary>The model version the report is checked against; null while the model has no published version.</summary>
    public required CalculationModelVersionRecord? Model { get; init; }

    /// <summary>The model version's variables.</summary>
    public required IReadOnlyList<VariableRecord> Variables { get; init; }

    /// <summary>The model version's calculated measures.</summary>
    public required IReadOnlyList<CalculatedMeasureRecord> CalculatedMeasures { get; init; }

    /// <summary>The <c>report_key</c> of the report.</summary>
    public string ReportKey => Report.ReportKey;
}
