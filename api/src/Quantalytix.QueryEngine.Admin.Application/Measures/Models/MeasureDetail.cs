using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// A measure with its components, the measures its formula depends on and its time policy, joined from <c>qe_measure</c>,
/// <c>qe_measure_component</c>, <c>qe_measure_dependency</c> and <c>qe_measure_time_policy</c>.
/// </summary>
public sealed record MeasureDetail
{
    /// <summary>The measure.</summary>
    public required MeasureRecord Measure { get; init; }

    /// <summary>The fields the measure reads, by role.</summary>
    public required IReadOnlyList<MeasureComponentRecord> Components { get; init; }

    /// <summary>The measures of the version the formula references, derived from its expression; empty for other kinds.</summary>
    public required IReadOnlyList<long> DependencyMeasureIds { get; init; }

    /// <summary>The time policy, or null when the measure has none.</summary>
    public MeasureTimePolicyRecord? TimePolicy { get; init; }
}
