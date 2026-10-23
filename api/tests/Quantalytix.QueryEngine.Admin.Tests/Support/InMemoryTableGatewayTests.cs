using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Support;

/// <summary>
/// Confirms that <see cref="InMemoryTableGateway{TRecord}"/> answers like the StarRocks gateway: inserts upsert by the full
/// primary key, filters match by SQL equality or IN (an empty collection and a null value match nothing), updates replace the
/// row with the record's key, and deletes remove the filtered rows. Feature service tests run against it instead of the
/// database, so any difference would let them pass on behavior the database does not have.
/// </summary>
public sealed class InMemoryTableGatewayTests
{
    /// <summary>A root member of dimension version 7.</summary>
    private static readonly DimensionMemberRecord California = Member(1, 7, "CA", null);

    /// <summary>A child member of dimension version 7.</summary>
    private static readonly DimensionMemberRecord NewYork = Member(2, 7, "NY", 1);

    /// <summary>A member of dimension version 8 with the same key as <see cref="California"/>.</summary>
    private static readonly DimensionMemberRecord LaterCalifornia = Member(3, 8, "CA", null);

    /// <summary>An insert replaces the row whose whole composite key matches and adds the others.</summary>
    /// <returns>A task that completes when the rows are checked.</returns>
    [Fact]
    public async Task Insert_UpsertsByTheWholeKey()
    {
        var closure = new InMemoryTableGateway<DimensionClosureRecord>(Closure(10, 11, 1), Closure(10, 12, 1));

        await closure.InsertManyAsync([Closure(10, 11, 5), Closure(11, 12, 1)], TestContext.Current.CancellationToken);

        Assert.Equal([Closure(10, 11, 5), Closure(10, 12, 1), Closure(11, 12, 1)], closure.All);
    }

    /// <summary>Filters match by SQL equality, with numbers compared by value, and collections other than strings match with IN.</summary>
    /// <returns>A task that completes when the filters are checked.</returns>
    [Fact]
    public async Task List_FiltersLikeSql()
    {
        var members = new InMemoryTableGateway<DimensionMemberRecord>(California, NewYork, LaterCalifornia);
        var token = TestContext.Current.CancellationToken;

        Assert.Equal([California, NewYork], await members.ListAsync(new { DimensionVersionId = 7 }, token));
        Assert.Equal([California, LaterCalifornia], await members.ListAsync(new { MemberKey = "CA" }, token));
        Assert.Equal([California, LaterCalifornia], await members.ListAsync(new { DimensionMemberId = new long[] { 1, 3 } }, token));
        Assert.Empty(await members.ListAsync(new { DimensionMemberId = Array.Empty<long>() }, token));
        Assert.Empty(await members.ListAsync(new { ParentDimensionMemberId = (long?)null }, token));
        Assert.Equal(NewYork, await members.GetAsync(new { DimensionMemberId = 2L }, token));
    }

    /// <summary>An update replaces the row with the record's key and changes nothing for a missing key; a delete removes the filtered rows.</summary>
    /// <returns>A task that completes when the rows are checked.</returns>
    [Fact]
    public async Task UpdateAndDelete_ChangeOnlyTheirRows()
    {
        var members = new InMemoryTableGateway<DimensionMemberRecord>(California, NewYork, LaterCalifornia);
        var token = TestContext.Current.CancellationToken;
        var renamed = California with { DisplayName = "California (renamed)" };

        await members.UpdateAsync(renamed, token);
        await members.UpdateAsync(Member(99, 7, "TX", null), token);
        await members.DeleteAsync(new { DimensionMemberId = new long[] { 2, 3 } }, token);

        Assert.Equal([renamed], members.All);
    }

    /// <summary>A member with its key as display name.</summary>
    private static DimensionMemberRecord Member(long dimensionMemberId, long dimensionVersionId, string memberKey, long? parentDimensionMemberId)
    {
        return new DimensionMemberRecord
        {
            DimensionMemberId = dimensionMemberId,
            DimensionVersionId = dimensionVersionId,
            MemberKey = memberKey,
            DisplayName = memberKey,
            ParentDimensionMemberId = parentDimensionMemberId,
            IsAssignable = true,
            SortOrder = 0
        };
    }

    /// <summary>A closure row of dimension version 7.</summary>
    private static DimensionClosureRecord Closure(long ancestorDimensionMemberId, long descendantDimensionMemberId, int depth)
    {
        return new DimensionClosureRecord
        {
            DimensionVersionId = 7,
            AncestorDimensionMemberId = ancestorDimensionMemberId,
            DescendantDimensionMemberId = descendantDimensionMemberId,
            Depth = depth
        };
    }
}
