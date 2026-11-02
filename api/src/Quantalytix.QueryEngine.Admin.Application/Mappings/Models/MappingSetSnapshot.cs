using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// The mapping set aggregate in memory: the identity row and one version's entries (the live version for a change set), with what
/// surrounds it as stored. Operations, rules, delete checks and read models all work on these values.
/// </summary>
public sealed record MappingSetSnapshot
{
    /// <summary>The mapping set.</summary>
    public required MappingSetRecord Set { get; init; }

    /// <summary>The version whose entries the snapshot holds.</summary>
    public required MappingVersionRecord Version { get; init; }

    /// <summary>The version's entries.</summary>
    public required IReadOnlyList<DimensionMappingRecord> Entries { get; init; }

    /// <summary>What surrounds the aggregate, read when it was loaded.</summary>
    public required MappingSetSurroundings Surroundings { get; init; }

    /// <summary>The version's id.</summary>
    public long VersionId => Version.MappingVersionId;

    /// <summary>Whether the version is the live one, which the engine reads and change sets edit.</summary>
    public bool IsLive => Version.SnapshotId == Surroundings.LiveSnapshotId;
}
