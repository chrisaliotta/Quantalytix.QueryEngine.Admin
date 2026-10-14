using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Every child row of one or more dataset versions, as cloning and draft deletion read them.</summary>
public sealed record DatasetVersionContent
{
    /// <summary>The fields.</summary>
    public required IReadOnlyList<FieldRecord> Fields { get; init; }

    /// <summary>The grains.</summary>
    public required IReadOnlyList<DatasetGrainRecord> Grains { get; init; }

    /// <summary>The grains' component fields.</summary>
    public required IReadOnlyList<DatasetGrainFieldRecord> GrainFields { get; init; }

    /// <summary>The dimension bindings.</summary>
    public required IReadOnlyList<DimensionBindingRecord> Bindings { get; init; }

    /// <summary>The time extensions of the time bindings.</summary>
    public required IReadOnlyList<TimeBindingRecord> TimeBindings { get; init; }

    /// <summary>The measures.</summary>
    public required IReadOnlyList<MeasureRecord> Measures { get; init; }

    /// <summary>The measures' components.</summary>
    public required IReadOnlyList<MeasureComponentRecord> Components { get; init; }

    /// <summary>The measures' formula dependencies.</summary>
    public required IReadOnlyList<MeasureDependencyRecord> Dependencies { get; init; }

    /// <summary>The measures' time policies.</summary>
    public required IReadOnlyList<MeasureTimePolicyRecord> TimePolicies { get; init; }
}
