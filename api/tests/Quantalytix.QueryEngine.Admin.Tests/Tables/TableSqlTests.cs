using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Tables;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Tables;

/// <summary>
/// Confirms the statements the table gateway sends: SELECT and DELETE with equality or IN conditions, one multi-row INSERT
/// with per-row parameters and PARSE_JSON around JSON columns, and UPDATE with the key only in WHERE. Every write of every
/// feature goes through these statements, and StarRocks stores JSON text as JSON only through PARSE_JSON.
/// </summary>
public sealed class TableSqlTests
{
    /// <summary>The columns of <c>qe_dimension_member</c>, which has a JSON column, in DDL order.</summary>
    private const string MemberColumns =
        "member_id, dimension_version_id, member_key, display_name, description, parent_member_id, level_id, is_assignable, sort_order, properties";

    /// <summary>Without a filter, SELECT reads every column of every row.</summary>
    [Fact]
    public void Select_WithoutFilter_ReadsEveryRow()
    {
        Assert.Equal($"SELECT {MemberColumns} FROM qe_dimension_member", TableSql<DimensionMemberRecord>.Select(null));
    }

    /// <summary>A collection filters with IN, a string or a single value with equality, and an empty collection matches nothing.</summary>
    [Fact]
    public void Select_FiltersCollectionsWithIn()
    {
        var filter = new { DimensionVersionId = 7L, MemberKey = "CA", MemberId = new long[] { 1, 2 } };
        Assert.Equal(
            $"SELECT {MemberColumns} FROM qe_dimension_member WHERE dimension_version_id = @DimensionVersionId AND member_key = @MemberKey AND member_id IN @MemberId",
            TableSql<DimensionMemberRecord>.Select(filter));
        Assert.False(TableFilter.MatchesNothing(filter));
        Assert.True(TableFilter.MatchesNothing(new { MemberId = Array.Empty<long>() }));
    }

    /// <summary>One INSERT writes every row, naming each row's parameters <c>p{row}_{Property}</c> and parsing JSON columns.</summary>
    [Fact]
    public void Insert_WritesEveryRowWithRowParameters()
    {
        Assert.Equal(
            $"INSERT INTO qe_dimension_member ({MemberColumns}) VALUES "
            + "(@p0_MemberId, @p0_DimensionVersionId, @p0_MemberKey, @p0_DisplayName, @p0_Description, @p0_ParentMemberId, @p0_LevelId, "
            + "@p0_IsAssignable, @p0_SortOrder, PARSE_JSON(@p0_Properties)), "
            + "(@p1_MemberId, @p1_DimensionVersionId, @p1_MemberKey, @p1_DisplayName, @p1_Description, @p1_ParentMemberId, @p1_LevelId, "
            + "@p1_IsAssignable, @p1_SortOrder, PARSE_JSON(@p1_Properties))",
            TableSql<DimensionMemberRecord>.Insert(2));

        var parameters = TableSql<DimensionMemberRecord>.InsertParameters([Member(11, "CA", """{"region":"west"}"""), Member(12, "NY", null)]);
        Assert.Equal(20, parameters.Count);
        Assert.Equal(11L, parameters["p0_MemberId"]);
        Assert.Equal("""{"region":"west"}""", parameters["p0_Properties"]);
        Assert.Equal("NY", parameters["p1_MemberKey"]);
        Assert.Null(parameters["p1_Properties"]);
    }

    /// <summary>UPDATE sets every non-key column, parsing JSON columns, and matches the key columns only in WHERE.</summary>
    [Fact]
    public void Update_SetsNonKeyColumnsAndMatchesTheKey()
    {
        Assert.Equal(
            "UPDATE qe_dimension_member SET dimension_version_id = @DimensionVersionId, member_key = @MemberKey, display_name = @DisplayName, "
            + "description = @Description, parent_member_id = @ParentMemberId, level_id = @LevelId, is_assignable = @IsAssignable, "
            + "sort_order = @SortOrder, properties = PARSE_JSON(@Properties) WHERE member_id = @MemberId",
            TableSql<DimensionMemberRecord>.Update);
        Assert.Equal(
            "UPDATE qe_dimension_closure SET depth = @Depth "
            + "WHERE dimension_version_id = @DimensionVersionId AND ancestor_member_id = @AncestorMemberId AND descendant_member_id = @DescendantMemberId",
            TableSql<DimensionClosureRecord>.Update);
    }

    /// <summary>DELETE removes the rows its filter matches, with the same equality and IN conditions as SELECT.</summary>
    [Fact]
    public void Delete_RemovesTheFilteredRows()
    {
        Assert.Equal(
            "DELETE FROM qe_dimension_member WHERE dimension_version_id = @DimensionVersionId AND member_id IN @MemberId",
            TableSql<DimensionMemberRecord>.Delete(new { DimensionVersionId = 7L, MemberId = new long[] { 1, 2 } }));
    }

    /// <summary>A member of dimension version 7.</summary>
    private static DimensionMemberRecord Member(long memberId, string memberKey, string? properties)
    {
        return new DimensionMemberRecord
        {
            MemberId = memberId,
            DimensionVersionId = 7,
            MemberKey = memberKey,
            DisplayName = memberKey,
            IsAssignable = true,
            SortOrder = 0,
            Properties = properties
        };
    }
}
