using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Tests.Bridges;

/// <summary>Builds the bridge metadata rows the Bridges tests seed their in-memory tables with.</summary>
internal static class BridgeRows
{
    /// <summary>An enabled bridge between two dimensions, optionally pointing at a current version.</summary>
    public static BridgeRecord Bridge(long bridgeId, long sourceDimensionId, long targetDimensionId, long? currentVersionId = null)
    {
        return new BridgeRecord
        {
            BridgeId = bridgeId,
            BridgeKey = $"zz_map_bridge_{bridgeId}",
            DisplayName = $"Bridge {bridgeId}",
            SourceDimensionId = sourceDimensionId,
            TargetDimensionId = targetDimensionId,
            CurrentBridgeVersionId = currentVersionId,
            IsEnabled = true
        };
    }

    /// <summary>A version of a bridge with its endpoint versions, cardinality, allocation mode and status.</summary>
    public static BridgeVersionRecord BridgeVersion(
        long bridgeVersionId,
        long bridgeId,
        int versionNumber,
        long sourceVersionId,
        long targetVersionId,
        string cardinality,
        string allocationMode,
        string status)
    {
        return new BridgeVersionRecord
        {
            BridgeVersionId = bridgeVersionId,
            BridgeId = bridgeId,
            VersionNumber = versionNumber,
            SourceDimensionVersionId = sourceVersionId,
            TargetDimensionVersionId = targetVersionId,
            Cardinality = cardinality,
            AllocationMode = allocationMode,
            Status = status
        };
    }

    /// <summary>A mapping of a bridge version from a source member to a target member, with a weight and a factor of 1 by default.</summary>
    public static BridgeMappingRecord Mapping(long bridgeVersionId, long sourceMemberId, long targetMemberId, decimal allocationWeight, decimal factor = 1m)
    {
        return new BridgeMappingRecord
        {
            BridgeVersionId = bridgeVersionId,
            SourceMemberId = sourceMemberId,
            TargetMemberId = targetMemberId,
            Factor = factor,
            AllocationWeight = allocationWeight
        };
    }
}
