using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;

namespace Quantalytix.QueryEngine.Admin.Tests.Bridges;

/// <summary>Builds the bridge metadata rows the Bridges tests seed their in-memory tables with.</summary>
internal static class BridgeRows
{
    /// <summary>The next mapping ID, so every mapping row has its own key.</summary>
    private static long _nextMappingId;

    /// <summary>An enabled bridge between two dimensions.</summary>
    public static BridgeRecord Bridge(long bridgeId, long sourceDimensionId, long targetDimensionId)
    {
        return new BridgeRecord
        {
            BridgeId = bridgeId,
            BridgeKey = $"zz_map_bridge_{bridgeId}",
            DisplayName = $"Bridge {bridgeId}",
            SourceDimensionId = sourceDimensionId,
            TargetDimensionId = targetDimensionId
        };
    }

    /// <summary>An enabled version of a bridge in a snapshot, live by default, with its cardinality and allocation mode.</summary>
    public static BridgeVersionRecord BridgeVersion(long bridgeVersionId, long bridgeId, string cardinality, string allocationMode, long snapshotId = SnapshotRows.LiveId)
    {
        return new BridgeVersionRecord
        {
            CreatedAt = DateTime.UnixEpoch,
            BridgeVersionId = bridgeVersionId,
            BridgeId = bridgeId,
            SnapshotId = snapshotId,
            Cardinality = cardinality,
            AllocationMode = allocationMode,
            IsEnabled = true
        };
    }

    /// <summary>A mapping of a bridge version from a source member key to a target member key, with a weight and a factor of 1 by default.</summary>
    public static BridgeMappingRecord Mapping(long bridgeVersionId, string sourceMemberKey, string targetMemberKey, decimal allocationWeight, decimal factor = 1m)
    {
        return new BridgeMappingRecord
        {
            BridgeMappingId = Interlocked.Increment(ref _nextMappingId),
            BridgeVersionId = bridgeVersionId,
            SourceMemberKey = sourceMemberKey,
            TargetMemberKey = targetMemberKey,
            Factor = factor,
            AllocationWeight = allocationWeight,
            SourceSelection = SourceSelections.Member
        };
    }
}
