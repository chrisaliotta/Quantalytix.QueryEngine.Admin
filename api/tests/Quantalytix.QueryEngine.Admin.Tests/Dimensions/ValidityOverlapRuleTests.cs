using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Dimensions.DimensionRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms <c>validity_overlap</c>: a draft's half-open window may touch but not overlap a published version's, except
/// for the open-ended published predecessor that starts earlier, which publishing closes. The engine resolves an unpinned
/// binding to the single published version whose window contains the request time, so overlapping windows would make that
/// resolution ambiguous.
/// </summary>
public sealed class ValidityOverlapRuleTests
{
    /// <summary>The dimension the versions belong to.</summary>
    private const long DimensionId = 1;

    /// <summary>The draft being validated.</summary>
    private const long DraftId = 12;

    /// <summary>A window that starts where the published one ends passes, and drafts of the dimension are ignored.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task TouchingWindows_Pass()
    {
        var violations = await EvaluateAsync(
            Version(11, DimensionId, 1, PublicationStatuses.Published, Utc(2025, 1, 1), Utc(2026, 1, 1)),
            Version(13, DimensionId, 3, PublicationStatuses.Draft, Utc(2026, 2, 1), null),
            Version(DraftId, DimensionId, 2, PublicationStatuses.Draft, Utc(2026, 1, 1), null));

        Assert.Empty(violations);
    }

    /// <summary>A window that starts before a closed published window ends overlaps it.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task OverlappingWindow_Fails()
    {
        var violations = await EvaluateAsync(
            Version(11, DimensionId, 1, PublicationStatuses.Published, Utc(2025, 1, 1), Utc(2026, 1, 1)),
            Version(DraftId, DimensionId, 2, PublicationStatuses.Draft, Utc(2025, 6, 1), null));

        var violation = Assert.Single(violations);
        Assert.Equal(ValidityOverlapRule.Code, violation.Rule);
        Assert.Equal("v2 [2025-06-01 .. open) overlaps published v1 [2025-01-01 .. 2026-01-01).", violation.Message);
    }

    /// <summary>An open-ended published predecessor that starts earlier is allowed, because publishing the draft closes it.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task OpenEndedPredecessor_IsAllowed()
    {
        var violations = await EvaluateAsync(
            Version(11, DimensionId, 1, PublicationStatuses.Published, Utc(2025, 1, 1), null),
            Version(DraftId, DimensionId, 2, PublicationStatuses.Draft, Utc(2026, 1, 1), null));

        Assert.Empty(violations);
    }

    /// <summary>An open-ended published version that starts later is no predecessor, so a draft window reaching into it overlaps.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task OpenEndedLaterVersion_Fails()
    {
        var violations = await EvaluateAsync(
            Version(11, DimensionId, 1, PublicationStatuses.Published, Utc(2026, 1, 1), null),
            Version(DraftId, DimensionId, 2, PublicationStatuses.Draft, Utc(2025, 1, 1), Utc(2026, 6, 1)));

        Assert.Equal([ValidityOverlapRule.Code], violations.Select(violation => violation.Rule));
    }

    /// <summary>Runs the rule for the draft over the given versions.</summary>
    private static Task<IReadOnlyList<RuleViolation>> EvaluateAsync(params DimensionVersionRecord[] versions)
    {
        var rule = new ValidityOverlapRule(new InMemoryTableGateway<DimensionVersionRecord>(versions));
        return rule.EvaluateAsync(DraftId, TestContext.Current.CancellationToken);
    }
}
