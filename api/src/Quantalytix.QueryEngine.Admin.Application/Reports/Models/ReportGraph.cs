using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// A report version with the content of the model version it is checked against, which the report rules follow: its model's
/// published version, or the model version being published. The model content is empty while the model has no published
/// version, which <c>model_version_published</c> reports.
/// </summary>
public sealed record ReportGraph
{
    /// <summary>The <c>report_key</c> of the report.</summary>
    public required string ReportKey { get; init; }

    /// <summary>The report version.</summary>
    public required ReportVersionRecord Version { get; init; }

    /// <summary>The version's columns.</summary>
    public required IReadOnlyList<ReportColumnRecord> Columns { get; init; }

    /// <summary>The version's lines.</summary>
    public required IReadOnlyList<ReportLineRecord> Lines { get; init; }

    /// <summary>The model version the report is checked against; null while the model has no published version.</summary>
    public required CalculationModelVersionRecord? Model { get; init; }

    /// <summary>The model version's variables.</summary>
    public required IReadOnlyList<VariableRecord> Variables { get; init; }

    /// <summary>The model version's calculated measures.</summary>
    public required IReadOnlyList<CalculatedMeasureRecord> CalculatedMeasures { get; init; }

    /// <summary>The model version's source profiles.</summary>
    public required IReadOnlyList<SourceProfileRecord> Profiles { get; init; }

    /// <summary>The model version's enabled sources, which its profiles may pick.</summary>
    public required IReadOnlyList<VariableSourceRecord> Sources { get; init; }

    /// <summary>The model version's enabled calculation dimensions.</summary>
    public required IReadOnlyList<CalculationDimensionRecord> Dimensions { get; init; }

    /// <summary>The model version's routes.</summary>
    public required IReadOnlyList<CalculationRouteRecord> Routes { get; init; }
}
