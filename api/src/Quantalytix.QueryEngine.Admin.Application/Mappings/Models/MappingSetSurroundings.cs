using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// The rows outside a mapping set aggregate that its operations, rules and read models consult, read once when it loads: a change
/// set's operations apply in memory and cannot read.
/// </summary>
public sealed record MappingSetSurroundings
{
    /// <summary>The dimension the entries map into.</summary>
    public required DimensionRecord Dimension { get; init; }

    /// <summary>The keys of the dimension's version in the mapping version's snapshot, which entries resolve in; null when it holds none.</summary>
    public required DimensionKeys? DimensionKeys { get; init; }

    /// <summary>The live snapshot, which tells whether the version may change.</summary>
    public required long LiveSnapshotId { get; init; }

    /// <summary>Every version of the mapping set as stored, one per snapshot.</summary>
    public required IReadOnlyList<MappingVersionRecord> Versions { get; init; }

    /// <summary>The keys of the frozen snapshots holding a version of the mapping set, which block deleting it.</summary>
    public required IReadOnlyList<string> FrozenSnapshotKeys { get; init; }

    /// <summary>The entry count of every version, by version id.</summary>
    public required IReadOnlyDictionary<long, int> EntryCounts { get; init; }

    /// <summary>The dataset bindings of the version's snapshot that classify through the mapping set.</summary>
    public required IReadOnlyList<MappingBindingUsage> Bindings { get; init; }
}
