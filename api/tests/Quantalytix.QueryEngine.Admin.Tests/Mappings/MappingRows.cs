using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Tests.Support;

namespace Quantalytix.QueryEngine.Admin.Tests.Mappings;

/// <summary>Builds the mapping metadata rows the Mappings tests seed their in-memory tables with.</summary>
internal static class MappingRows
{
    /// <summary>A mapping set onto a dimension.</summary>
    public static MappingSetRecord MappingSet(long mappingSetId, long dimensionId)
    {
        return new MappingSetRecord
        {
            MappingSetId = mappingSetId,
            MappingSetKey = $"zz_map_codes_{mappingSetId}",
            SourceNamespace = "core_banking",
            DimensionId = dimensionId,
            DisplayName = $"Codes {mappingSetId}"
        };
    }

    /// <summary>A version of a mapping set in a snapshot, live by default.</summary>
    public static MappingVersionRecord MappingVersion(long mappingVersionId, long mappingSetId, long snapshotId = SnapshotRows.LiveId)
    {
        return new MappingVersionRecord
        {
            CreatedAt = DateTime.UnixEpoch,
            MappingVersionId = mappingVersionId,
            MappingSetId = mappingSetId,
            SnapshotId = snapshotId
        };
    }

    /// <summary>A mapping entry from a source value to a member key.</summary>
    public static DimensionMappingRecord Entry(long dimensionMappingId, long mappingVersionId, string sourceKeyValue, string memberKey)
    {
        return new DimensionMappingRecord
        {
            DimensionMappingId = dimensionMappingId,
            MappingVersionId = mappingVersionId,
            SourceKeyValue = sourceKeyValue,
            MemberKey = memberKey
        };
    }
}
