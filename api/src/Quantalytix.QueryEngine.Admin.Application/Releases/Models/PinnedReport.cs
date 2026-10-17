using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>A report a release pins, with the content of the model version its pinned version reads, for the release rules.</summary>
public sealed record PinnedReport
{
    /// <summary>The <c>report_key</c> of the report.</summary>
    public required string ReportKey { get; init; }

    /// <summary>The pinned report version.</summary>
    public required ReportVersionRecord Version { get; init; }

    /// <summary>The version's lines.</summary>
    public required IReadOnlyList<ReportLineRecord> Lines { get; init; }

    /// <summary>The model version's variables.</summary>
    public required IReadOnlyList<VariableRecord> Variables { get; init; }

    /// <summary>The model version's enabled sources, which its profiles may pick.</summary>
    public required IReadOnlyList<VariableSourceRecord> Sources { get; init; }

    /// <summary>The model version's enabled calculation dimensions.</summary>
    public required IReadOnlyList<CalculationDimensionRecord> Dimensions { get; init; }

    /// <summary>The model version's routes.</summary>
    public required IReadOnlyList<CalculationRouteRecord> Routes { get; init; }
}
