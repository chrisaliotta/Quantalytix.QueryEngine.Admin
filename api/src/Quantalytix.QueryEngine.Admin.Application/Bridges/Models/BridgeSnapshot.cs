using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// The bridge aggregate in memory: the identity row and one version's settings and mappings (the live version for a change set),
/// with what surrounds it as stored. Operations, rules, delete checks and read models all work on these values.
/// </summary>
public sealed record BridgeSnapshot : IVersionSnapshot
{
    /// <summary>The bridge.</summary>
    public required BridgeRecord Bridge { get; init; }

    /// <summary>The version whose settings and mappings the snapshot holds.</summary>
    public required BridgeVersionRecord Version { get; init; }

    /// <summary>The version's mappings.</summary>
    public required IReadOnlyList<BridgeMappingRecord> Mappings { get; init; }

    /// <summary>What surrounds the aggregate, read when it was loaded.</summary>
    public required BridgeSurroundings Surroundings { get; init; }

    /// <summary><c>bridge_version</c>.</summary>
    public string Kind => VersionKinds.BridgeVersion;

    /// <summary>The version's id.</summary>
    public long VersionId => Version.BridgeVersionId;

    /// <summary>Whether the version is the live one, which the engine reads and change sets edit.</summary>
    public bool IsLive => Version.Status == PublicationStatuses.Published;

    /// <summary>
    /// Whether the bridge maps into a report's dimension, which makes it a report source's bridge (B9) from its creation on, before the
    /// source stores its id.
    /// </summary>
    public bool IsReportBridge => Surroundings.Target.DimensionPurpose == DimensionPurposes.Report;
}
