using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>Builds the dimension metadata rows the Dimensions tests seed their in-memory tables with.</summary>
internal static class DimensionRows
{
    /// <summary>A user-defined dimension of a kind.</summary>
    public static DimensionRecord Dimension(long dimensionId, string kind)
    {
        return new DimensionRecord
        {
            DimensionId = dimensionId,
            DimensionKey = $"zz_dim_{dimensionId}",
            DisplayName = $"Dimension {dimensionId}",
            DimensionKind = kind,
            DefinitionOrigin = DefinitionOrigins.User,
            IsRemovable = true,
            IsEnabled = true
        };
    }

    /// <summary>A role of a dimension, whose key is also its display name.</summary>
    public static DimensionRoleRecord Role(long roleId, string roleKey, long dimensionId)
    {
        return new DimensionRoleRecord { RoleId = roleId, DimensionId = dimensionId, RoleKey = roleKey, DisplayName = roleKey };
    }

    /// <summary>A version of a dimension with a status and an optional range data type.</summary>
    public static DimensionVersionRecord Version(long versionId, long dimensionId, int versionNumber, string status, string? rangeDataType = null)
    {
        return new DimensionVersionRecord
        {
            DimensionVersionId = versionId,
            DimensionId = dimensionId,
            VersionNumber = versionNumber,
            Status = status,
            RangeDataType = rangeDataType
        };
    }

    /// <summary>A level of a version.</summary>
    public static DimensionLevelRecord Level(long levelId, long versionId, string levelKey, int ordinal)
    {
        return new DimensionLevelRecord
        {
            LevelId = levelId,
            DimensionVersionId = versionId,
            LevelKey = levelKey,
            DisplayName = levelKey,
            Ordinal = ordinal,
            IsLeafLevel = false
        };
    }

    /// <summary>A member of a version, optionally under a parent and on a level.</summary>
    public static DimensionMemberRecord Member(long memberId, long versionId, string memberKey, long? parentId = null, long? levelId = null)
    {
        return new DimensionMemberRecord
        {
            MemberId = memberId,
            DimensionVersionId = versionId,
            MemberKey = memberKey,
            DisplayName = memberKey,
            ParentMemberId = parentId,
            LevelId = levelId,
            IsAssignable = true,
            SortOrder = 0
        };
    }

    /// <summary>A decimal band; by default the lower bound is inclusive and the upper one exclusive, like the column defaults.</summary>
    public static RangeBandRecord DecimalBand(
        long versionId,
        int ordinal,
        long memberId,
        decimal? lower,
        decimal? upper,
        bool lowerInclusive = true,
        bool upperInclusive = false)
    {
        return new RangeBandRecord
        {
            DimensionVersionId = versionId,
            Ordinal = ordinal,
            MemberId = memberId,
            LowerDecimal = lower,
            UpperDecimal = upper,
            LowerInclusive = lowerInclusive,
            UpperInclusive = upperInclusive
        };
    }

    /// <summary>Midnight UTC on a date.</summary>
    public static DateTime Utc(int year, int month, int day)
    {
        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
    }
}
