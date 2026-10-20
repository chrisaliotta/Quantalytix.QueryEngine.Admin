using Quantalytix.QueryEngine.Admin.Application.Metadata;

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
            TargetDimensionId = targetDimensionId,
            IsEnabled = true
        };
    }

    /// <summary>A version of a bridge with its cardinality, allocation mode and status.</summary>
    public static BridgeVersionRecord BridgeVersion(long bridgeVersionId, long bridgeId, int versionNumber, string cardinality, string allocationMode, string status)
    {
        return new BridgeVersionRecord
        {
            BridgeVersionId = bridgeVersionId,
            BridgeId = bridgeId,
            VersionNumber = versionNumber,
            Cardinality = cardinality,
            AllocationMode = allocationMode,
            Status = status
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
            AllocationWeight = allocationWeight
        };
    }
}
