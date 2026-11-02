using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The dataset version aggregate in memory: the dataset row, one version's rows (settings, fields, grains, bindings with their
/// time axes, measures with their components, dependencies and time policies), the managed-table registry its renames write, and
/// what surrounds it as loaded. Operations, rules, delete checks and read models all work on these values.
/// </summary>
public sealed record DatasetSnapshot
{
    /// <summary>The dataset identity every version shares.</summary>
    public required DatasetRecord Dataset { get; init; }

    /// <summary>The version whose content the snapshot holds.</summary>
    public required DatasetVersionRecord Version { get; init; }

    /// <summary>The version's fields.</summary>
    public required IReadOnlyList<FieldRecord> Fields { get; init; }

    /// <summary>The version's grains.</summary>
    public required IReadOnlyList<DatasetGrainRecord> Grains { get; init; }

    /// <summary>The components of the version's grains.</summary>
    public required IReadOnlyList<DatasetGrainFieldRecord> GrainFields { get; init; }

    /// <summary>The version's bindings.</summary>
    public required IReadOnlyList<DimensionBindingRecord> Bindings { get; init; }

    /// <summary>The time axes of the version's time bindings.</summary>
    public required IReadOnlyList<TimeBindingRecord> TimeBindings { get; init; }

    /// <summary>The version's measures.</summary>
    public required IReadOnlyList<MeasureRecord> Measures { get; init; }

    /// <summary>The components of the version's measures.</summary>
    public required IReadOnlyList<MeasureComponentRecord> Components { get; init; }

    /// <summary>The formula dependencies of the version's measures, derived from their formulas.</summary>
    public required IReadOnlyList<MeasureDependencyRecord> Dependencies { get; init; }

    /// <summary>The time policies of the version's measures.</summary>
    public required IReadOnlyList<MeasureTimePolicyRecord> TimePolicies { get; init; }

    /// <summary>Every managed table of the tenant (OI-33), as the change set leaves them.</summary>
    public required IReadOnlyList<ManagedSourceObjectRecord> ManagedObjects { get; init; }

    /// <summary>What surrounds the aggregate, read when it was loaded.</summary>
    public required DatasetSurroundings Surroundings { get; init; }

    /// <summary>The version's id.</summary>
    public long VersionId => Version.DatasetVersionId;

    /// <summary>Whether the version belongs to the live snapshot, the only one whose content changes.</summary>
    public bool IsLive => Version.SnapshotId == Surroundings.LiveSnapshotId;

    /// <summary>The columns of the version's source object; none when the object does not exist.</summary>
    public IReadOnlyList<PhysicalColumn> SourceColumns => Surroundings.Columns.GetValueOrDefault(Version.SourceObject) ?? [];

    /// <summary>The registry row of the version's source object, or null when the API did not create it.</summary>
    public ManagedSourceObjectRecord? ManagedSource =>
        ManagedObjects.SingleOrDefault(managed => managed.SourceObject == Version.SourceObject && managed.SourceObjectKind == Version.SourceObjectKind);
}
