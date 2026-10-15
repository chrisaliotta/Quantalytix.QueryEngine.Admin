using System.Collections.Frozen;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// The lifecycle every versioned kind follows. Most kinds move draft → validated → published → retired and may return
/// from validated to draft; assumption sets have no validated state and move draft → published → retired. Leaving draft
/// runs the publication rules, and retiring runs the retirement rules.
/// </summary>
public static class VersionTransitions
{
    /// <summary>The allowed (from, to) pairs of every kind except assumption sets.</summary>
    private static readonly FrozenSet<(string From, string To)> Standard = FrozenSet.Create(
        (PublicationStatuses.Draft, PublicationStatuses.Validated),
        (PublicationStatuses.Validated, PublicationStatuses.Draft),
        (PublicationStatuses.Validated, PublicationStatuses.Published),
        (PublicationStatuses.Published, PublicationStatuses.Retired));

    /// <summary>The allowed (from, to) pairs of assumption sets.</summary>
    private static readonly FrozenSet<(string From, string To)> AssumptionSet = FrozenSet.Create(
        (AssumptionSetStatuses.Draft, AssumptionSetStatuses.Published),
        (AssumptionSetStatuses.Published, AssumptionSetStatuses.Retired));

    /// <summary>Whether a kind may move from one status to another.</summary>
    /// <param name="kind">A <c>version_kind</c> value.</param>
    /// <param name="from">The current status.</param>
    /// <param name="to">The target status.</param>
    /// <returns>True when the lifecycle allows the transition.</returns>
    public static bool IsAllowed(string kind, string from, string to)
    {
        if (kind == VersionKinds.AssumptionSet) return AssumptionSet.Contains((from, to));
        return Standard.Contains((from, to));
    }

    /// <summary>Whether a transition runs the lifecycle handlers and publication rules before it applies.</summary>
    /// <param name="kind">A <c>version_kind</c> value.</param>
    /// <param name="from">The current status.</param>
    /// <param name="to">The target status.</param>
    /// <returns>True for draft → validated, and for draft → published of an assumption set.</returns>
    public static bool RunsPublicationRules(string kind, string from, string to)
    {
        if (kind == VersionKinds.AssumptionSet) return from == AssumptionSetStatuses.Draft && to == AssumptionSetStatuses.Published;
        return from == PublicationStatuses.Draft && to == PublicationStatuses.Validated;
    }

    /// <summary>Whether a transition runs the retirement rules before it applies; every kind retires the same way.</summary>
    /// <param name="from">The current status.</param>
    /// <param name="to">The target status.</param>
    /// <returns>True for published → retired.</returns>
    public static bool RunsRetirementRules(string from, string to)
    {
        return from == PublicationStatuses.Published && to == PublicationStatuses.Retired;
    }
}
