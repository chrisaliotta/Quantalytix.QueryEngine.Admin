using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Changes;

/// <summary>
/// Confirms the commit write order (blueprint 01, "Writing without transactions", OI-03): StarRocks rejects UPDATE and DELETE
/// inside transactions, so a commit that stops midway must leave a harmless residue. Deletes run children-first, then updates,
/// then inserts parents-first, and the aggregate root's row is written last. Also confirms that <see cref="WritePlan.Sync"/>
/// turns stored rows into changed rows with only the writes needed.
/// </summary>
public sealed class WritePlanTests
{
    /// <summary>The root row's table.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _versions = new();

    /// <summary>A child table.</summary>
    private readonly InMemoryTableGateway<DimensionLevelRecord> _levels = new();

    /// <summary>A grandchild table.</summary>
    private readonly InMemoryTableGateway<DimensionMemberRecord> _members = new();

    /// <summary>Writes added in any order come out deletes deepest first, updates and inserts shallowest first, the root last.</summary>
    [Fact]
    public void Ordered_FollowsTheResidueSafeOrder()
    {
        var plan = new WritePlan();
        plan.Update(_versions, WritePlan.Root, Version());
        plan.Insert(_levels, 1, [Level(1)]);
        plan.Insert(_members, 2, [Member(1)]);
        plan.Delete(_levels, 1, new { DimensionLevelId = 2L });
        plan.Update(_members, 2, Member(3));
        plan.Delete(_members, 2, new { DimensionMemberId = 2L });
        plan.Update(_levels, 1, Level(3));

        Assert.Equal(
            [
                (WriteKind.Delete, 2, typeof(DimensionMemberRecord)),
                (WriteKind.Delete, 1, typeof(DimensionLevelRecord)),
                (WriteKind.Update, 1, typeof(DimensionLevelRecord)),
                (WriteKind.Update, 2, typeof(DimensionMemberRecord)),
                (WriteKind.Insert, 1, typeof(DimensionLevelRecord)),
                (WriteKind.Insert, 2, typeof(DimensionMemberRecord)),
                (WriteKind.Update, WritePlan.Root, typeof(DimensionVersionRecord))
            ],
            plan.Ordered().Select(write => (write.Kind, write.Depth, write.Record)));
    }

    /// <summary>Sync deletes the gone row, updates the changed one, inserts the new one, and leaves the unchanged one alone.</summary>
    /// <returns>A task that completes when the writes ran.</returns>
    [Fact]
    public async Task Sync_WritesOnlyTheDifference()
    {
        var members = new InMemoryTableGateway<DimensionMemberRecord>(Member(1), Member(2), Member(3));
        var plan = new WritePlan();

        plan.Sync(members, 2, members.All, [Member(1), Member(2) with { DisplayName = "Renamed" }, Member(4)], row => new { row.DimensionMemberId });
        foreach (var write in plan.Ordered()) await write.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal([WriteKind.Delete, WriteKind.Update, WriteKind.Insert], plan.Ordered().Select(write => write.Kind));
        Assert.Equal([1L, 2L, 4L], members.All.Select(row => row.DimensionMemberId).Order());
        Assert.Equal("Renamed", members.All.Single(row => row.DimensionMemberId == 2).DisplayName);
    }

    /// <summary>A dimension version row.</summary>
    private static DimensionVersionRecord Version()
    {
        return new DimensionVersionRecord { CreatedAt = DateTime.UnixEpoch, DimensionVersionId = 10, DimensionId = 1, VersionNumber = 1, Status = "published" };
    }

    /// <summary>A level row of version 10.</summary>
    private static DimensionLevelRecord Level(long id)
    {
        return new DimensionLevelRecord { DimensionLevelId = id, DimensionVersionId = 10, LevelKey = $"l{id}", DisplayName = $"L{id}", Ordinal = (int)id, IsLeafLevel = false };
    }

    /// <summary>A member row of version 10.</summary>
    internal static DimensionMemberRecord Member(long id, string? key = null)
    {
        return new DimensionMemberRecord
        {
            DimensionMemberId = id,
            DimensionVersionId = 10,
            MemberKey = key ?? $"m{id}",
            DisplayName = $"M{id}",
            IsAssignable = true,
            SortOrder = (int)id
        };
    }
}
