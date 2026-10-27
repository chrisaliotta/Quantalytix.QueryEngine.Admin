using FluentValidation;
using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Changes;

/// <summary>
/// Confirms the change-set protocol every module's editor relies on (blueprint 01, "Change-set protocol", OI-01): operations
/// apply in order to the aggregate as stored; the first one whose input fails its validator, or that cannot apply, refuses the
/// whole set with errors keyed by its index (<c>operations[1].key</c>); a gate refuses with 422; a refused commit writes nothing;
/// a preview writes nothing and reports publication findings; a commit writes the difference, advances the semantic revision
/// once when the engine reads the aggregate, and maps each temporary id to the real id it got. A small member-list aggregate
/// over one dimension version stands in for a module's.
/// </summary>
public sealed class ChangeSetPipelineTests
{
    /// <summary>The tenant's semantic revision before each test.</summary>
    private const long StartingRevision = 7;

    /// <summary>The first id the generator allocates.</summary>
    private const long FirstNewId = 900;

    /// <summary>The dimension version the aggregate is.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _versions = new(
        new DimensionVersionRecord { CreatedAt = DateTime.UnixEpoch, DimensionVersionId = 10, DimensionId = 1, VersionNumber = 1, Status = "published" });

    /// <summary>The version's members.</summary>
    private readonly InMemoryTableGateway<DimensionMemberRecord> _members = new(WritePlanTests.Member(1, "CA"), WritePlanTests.Member(2, "NY"));

    /// <summary>The tenant's database info row.</summary>
    private readonly InMemoryTableGateway<DatabaseInfoRecord> _databaseInfo = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 18,
        SemanticRevision = StartingRevision,
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>A commit adds, renames and removes members, maps the temporary id and advances the revision once.</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task Commit_WritesTheResultAndMapsTemporaryIds()
    {
        var outcome = await Pipeline().CommitAsync(10, Request(new AddMember("tmp:a", "TX"), new RenameMember("1", "CAL"), new RemoveMember("2")), Token);

        Assert.True(outcome.Succeeded);
        Assert.Equal(["CAL", "TX"], outcome.Value.State);
        Assert.Equal(new Dictionary<string, long> { ["tmp:a"] = FirstNewId }, outcome.Value.IdMap);
        Assert.Equal([(1L, "CAL"), (FirstNewId, "TX")], _members.All.Select(row => (row.DimensionMemberId, row.MemberKey)).Order());
        Assert.Equal(StartingRevision + 1, Revision());
    }

    /// <summary>A later operation may name an item an earlier one created by its temporary id.</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task Commit_ResolvesTemporaryIdsAcrossOperations()
    {
        var outcome = await Pipeline().CommitAsync(10, Request(new AddMember("tmp:a", "TX"), new RenameMember("tmp:a", "TEX")), Token);

        Assert.Equal(["CA", "NY", "TEX"], outcome.Value!.State);
    }

    /// <summary>A commit to an aggregate the engine does not read writes without advancing the revision.</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task Commit_OfAnEngineInvisibleChange_KeepsTheRevision()
    {
        await _versions.UpdateAsync(_versions.All[0] with { Status = "draft" }, Token);

        await Pipeline().CommitAsync(10, Request(new AddMember("tmp:a", "TX")), Token);

        Assert.Equal(StartingRevision, Revision());
    }

    /// <summary>An operation whose input fails its validator refuses the set with 400, keyed by the operation's index; nothing is written.</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task InvalidInput_RefusesKeyedByOperation()
    {
        var outcome = await Pipeline().CommitAsync(10, Request(new AddMember("tmp:a", "TX"), new AddMember("tmp:b", "")), Token);

        Assert.Equal(RefusalKind.Invalid, outcome.Refusal!.Kind);
        Assert.Equal(["operations[1].key"], outcome.Refusal.Errors.Keys);
        Assert.Equal(2, _members.All.Count);
    }

    /// <summary>An operation whose target is gone refuses the set with 409; nothing is written.</summary>
    /// <returns>A task that completes when the commit is checked.</returns>
    [Fact]
    public async Task OperationThatCannotApply_RefusesWithConflict()
    {
        var outcome = await Pipeline().CommitAsync(10, Request(new RenameMember("1", "CAL"), new RemoveMember("99")), Token);

        Assert.Equal(RefusalKind.Conflict, outcome.Refusal!.Kind);
        Assert.Equal("CA", _members.All.Single(row => row.DimensionMemberId == 1).MemberKey);
    }

    /// <summary>A result that breaks a gate refuses with 422, in commit and in preview alike; nothing is written.</summary>
    /// <returns>A task that completes when the commit and preview are checked.</returns>
    [Fact]
    public async Task GateViolation_RefusesWithRulesViolated()
    {
        var request = Request(new AddMember("tmp:a", "CA"));

        var commit = await Pipeline().CommitAsync(10, request, Token);
        var preview = await Pipeline().PreviewAsync(10, request, Token);

        Assert.Equal([KeysUniqueGate.Code], commit.Refusal!.Violations.Select(violation => violation.Code));
        Assert.Equal(RefusalKind.RulesViolated, preview.Refusal!.Kind);
        Assert.Equal(2, _members.All.Count);
    }

    /// <summary>A preview answers the pending state with the publication findings and writes nothing.</summary>
    /// <returns>A task that completes when the preview is checked.</returns>
    [Fact]
    public async Task Preview_AnswersThePendingStateAndFindingsWithoutWriting()
    {
        var outcome = await Pipeline().PreviewAsync(10, Request(new RemoveMember("1"), new RemoveMember("2")), Token);

        Assert.Empty(outcome.Value!.State);
        Assert.Equal([NotEmptyRule.Code], outcome.Value.Violations.Select(violation => violation.Code));
        Assert.Equal(2, _members.All.Count);
        Assert.Equal(StartingRevision, Revision());
    }

    /// <summary>The test's cancellation token.</summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A change set of the given operations.</summary>
    private static ChangeSetRequest<MemberOperation> Request(params MemberOperation[] operations)
    {
        return new ChangeSetRequest<MemberOperation> { Operations = operations };
    }

    /// <summary>The pipeline over the test aggregate, its gate and publication rule, and its operation validator.</summary>
    private ChangeSetPipeline<MemberList, MemberOperation, IReadOnlyList<string>> Pipeline()
    {
        return new ChangeSetPipeline<MemberList, MemberOperation, IReadOnlyList<string>>(
            new MemberListAggregate(_versions, _members),
            [new KeysUniqueGate(), new NotEmptyRule()],
            [new MemberOperationValidator()],
            new SequentialIdGenerator(FirstNewId),
            new SemanticRevision(_databaseInfo, new FakeTimeProvider()));
    }

    /// <summary>The tenant's semantic revision.</summary>
    private long Revision()
    {
        return Assert.Single(_databaseInfo.All).SemanticRevision;
    }

    /// <summary>The test aggregate's snapshot: a version and its members.</summary>
    /// <param name="Version">The version row, the root.</param>
    /// <param name="Members">The member rows.</param>
    private sealed record MemberList(DimensionVersionRecord Version, IReadOnlyList<DimensionMemberRecord> Members);

    /// <summary>An operation on the member list.</summary>
    private abstract record MemberOperation;

    /// <summary>Adds a member.</summary>
    /// <param name="Id">The client's temporary id.</param>
    /// <param name="Key">The member key.</param>
    private sealed record AddMember(string Id, string Key) : MemberOperation;

    /// <summary>Renames a member.</summary>
    /// <param name="Id">A stored or temporary member id.</param>
    /// <param name="Key">The new key.</param>
    private sealed record RenameMember(string Id, string Key) : MemberOperation;

    /// <summary>Removes a member.</summary>
    /// <param name="Id">A stored member id.</param>
    private sealed record RemoveMember(string Id) : MemberOperation;

    /// <summary>Requires an added member to have a key.</summary>
    private sealed class MemberOperationValidator : AbstractValidator<MemberOperation>
    {
        /// <summary>Declares the rule.</summary>
        public MemberOperationValidator()
        {
            RuleFor(operation => ((AddMember)operation).Key).NotEmpty().OverridePropertyName("key").When(operation => operation is AddMember);
        }
    }

    /// <summary>A gate: member keys are unique.</summary>
    private sealed class KeysUniqueGate : ISnapshotRule<MemberList>
    {
        /// <summary>The rule code.</summary>
        public const string Code = "member_key_unique";

        /// <summary>A gate.</summary>
        public RuleStage Stage => RuleStage.Gate;

        /// <summary>One violation per repeated key.</summary>
        /// <param name="snapshot">The member list.</param>
        /// <param name="cancellationToken">Unused.</param>
        /// <returns>The violations.</returns>
        public Task<IReadOnlyList<RuleViolation>> EvaluateAsync(MemberList snapshot, CancellationToken cancellationToken)
        {
            IReadOnlyList<RuleViolation> violations =
            [
                .. snapshot.Members.GroupBy(member => member.MemberKey).Where(group => group.Count() > 1)
                    .Select(group => new RuleViolation { Code = Code, Message = $"Key '{group.Key}' is used twice." })
            ];
            return Task.FromResult(violations);
        }
    }

    /// <summary>A publication rule: the list has members.</summary>
    private sealed class NotEmptyRule : ISnapshotRule<MemberList>
    {
        /// <summary>The rule code.</summary>
        public const string Code = "members_present";

        /// <summary>A publication rule.</summary>
        public RuleStage Stage => RuleStage.Publication;

        /// <summary>One violation when there are no members.</summary>
        /// <param name="snapshot">The member list.</param>
        /// <param name="cancellationToken">Unused.</param>
        /// <returns>The violations.</returns>
        public Task<IReadOnlyList<RuleViolation>> EvaluateAsync(MemberList snapshot, CancellationToken cancellationToken)
        {
            IReadOnlyList<RuleViolation> violations = snapshot.Members.Count == 0 ? [new RuleViolation { Code = Code, Message = "No members." }] : [];
            return Task.FromResult(violations);
        }
    }

    /// <summary>The test aggregate: a dimension version's members, edited by key.</summary>
    /// <param name="versions">The version table.</param>
    /// <param name="members">The member table.</param>
    private sealed class MemberListAggregate(ITableGateway<DimensionVersionRecord> versions, ITableGateway<DimensionMemberRecord> members)
        : IChangeSetAggregate<MemberList, MemberOperation, IReadOnlyList<string>>
    {
        /// <summary>Reads the version and its members.</summary>
        /// <param name="aggregateId">The version id.</param>
        /// <param name="cancellationToken">Cancels the reads.</param>
        /// <returns>The snapshot.</returns>
        public async Task<MemberList> LoadAsync(long aggregateId, CancellationToken cancellationToken)
        {
            var version = await versions.GetAsync(new { DimensionVersionId = aggregateId }, cancellationToken);
            return new MemberList(version, await members.ListAsync(new { DimensionVersionId = aggregateId }, cancellationToken));
        }

        /// <summary>Applies one operation; a removal of a member that is not there is a conflict.</summary>
        /// <param name="snapshot">The list so far.</param>
        /// <param name="operation">The operation.</param>
        /// <param name="context">Resolves ids and takes refusals.</param>
        /// <returns>The changed list.</returns>
        public MemberList Apply(MemberList snapshot, MemberOperation operation, ChangeContext context)
        {
            switch (operation)
            {
                case AddMember add:
                    return snapshot with { Members = [.. snapshot.Members, WritePlanTests.Member(context.Ids.Resolve(add.Id), add.Key)] };
                case RenameMember rename:
                    var id = context.Ids.Resolve(rename.Id);
                    return snapshot with { Members = [.. snapshot.Members.Select(member => member.DimensionMemberId == id ? member with { MemberKey = rename.Key } : member)] };
                default:
                    var removed = context.Ids.Resolve(((RemoveMember)operation).Id);
                    if (snapshot.Members.All(member => member.DimensionMemberId != removed))
                    {
                        context.Conflict("Member not found", $"Member {removed} is not in the version.");
                        return snapshot;
                    }

                    return snapshot with { Members = [.. snapshot.Members.Where(member => member.DimensionMemberId != removed)] };
            }
        }

        /// <summary>Syncs the member rows.</summary>
        /// <param name="before">The stored list.</param>
        /// <param name="after">The changed list.</param>
        /// <param name="plan">The plan.</param>
        public void Plan(MemberList before, MemberList after, WritePlan plan)
        {
            plan.Sync(members, 1, before.Members, after.Members, member => new { member.DimensionMemberId });
        }

        /// <summary>The engine reads a published version.</summary>
        /// <param name="before">The stored list.</param>
        /// <param name="after">The changed list.</param>
        /// <returns>Whether the version is published.</returns>
        public bool IsEngineVisible(MemberList before, MemberList after)
        {
            return before.Version.Status == "published";
        }

        /// <summary>The member keys, in order.</summary>
        /// <param name="snapshot">The list.</param>
        /// <param name="presentId">Unused; the read model holds keys.</param>
        /// <param name="cancellationToken">Unused.</param>
        /// <returns>The keys.</returns>
        public Task<IReadOnlyList<string>> ReadAsync(MemberList snapshot, Func<long, string> presentId, CancellationToken cancellationToken)
        {
            IReadOnlyList<string> keys = [.. snapshot.Members.Select(member => member.MemberKey).Order(StringComparer.Ordinal)];
            return Task.FromResult(keys);
        }
    }
}
