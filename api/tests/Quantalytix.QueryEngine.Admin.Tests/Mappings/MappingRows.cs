using Quantalytix.QueryEngine.Admin.Application.Metadata;

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

    /// <summary>A version of a mapping set pinned to a dimension version.</summary>
    public static MappingVersionRecord MappingVersion(long mappingVersionId, long mappingSetId, int versionNumber, long dimensionVersionId, string status)
    {
        return new MappingVersionRecord
        {
            MappingVersionId = mappingVersionId,
            MappingSetId = mappingSetId,
            VersionNumber = versionNumber,
            DimensionVersionId = dimensionVersionId,
            Status = status
        };
    }

    /// <summary>A mapping entry from a source value to a member.</summary>
    public static DimensionMappingRecord Entry(long mappingEntryId, long mappingVersionId, string sourceKeyValue, long memberId)
    {
        return new DimensionMappingRecord
        {
            MappingEntryId = mappingEntryId,
            MappingVersionId = mappingVersionId,
            SourceKeyValue = sourceKeyValue,
            MemberId = memberId
        };
    }
}
