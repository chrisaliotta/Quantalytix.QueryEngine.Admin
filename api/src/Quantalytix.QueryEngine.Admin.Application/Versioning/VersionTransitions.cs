using System.Collections.Frozen;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// The lifecycle every versioned kind follows: draft → validated → published, with a return from validated to draft. Strict
/// kinds are retired by hand; a living kind's published version is retired only when its successor is published. Leaving draft
/// runs the publication rules, and retiring runs the retirement rules.
/// </summary>
public static class VersionTransitions
{
    /// <summary>The allowed (from, to) pairs of the living kinds.</summary>
    private static readonly FrozenSet<(string From, string To)> Living = FrozenSet.Create(
        (PublicationStatuses.Draft, PublicationStatuses.Validated),
        (PublicationStatuses.Validated, PublicationStatuses.Draft),
        (PublicationStatuses.Validated, PublicationStatuses.Published));

    /// <summary>The allowed (from, to) pairs of the strict kinds: the living pairs and published → retired.</summary>
    private static readonly FrozenSet<(string From, string To)> Strict = FrozenSet.Create(
        [.. Living, (PublicationStatuses.Published, PublicationStatuses.Retired)]);

    /// <summary>Whether a kind may move from one status to another.</summary>
    /// <param name="kind">A <c>version_kind</c> value.</param>
    /// <param name="from">The current status.</param>
    /// <param name="to">The target status.</param>
    /// <returns>True when the lifecycle allows the transition.</returns>
    public static bool IsAllowed(string kind, string from, string to)
    {
        if (VersionKinds.Living.Contains(kind)) return Living.Contains((from, to));
        return Strict.Contains((from, to));
    }

    /// <summary>Whether a transition runs the lifecycle handlers and publication rules before it applies.</summary>
    /// <param name="from">The current status.</param>
    /// <param name="to">The target status.</param>
    /// <returns>True for draft → validated.</returns>
    public static bool RunsPublicationRules(string from, string to)
    {
        return from == PublicationStatuses.Draft && to == PublicationStatuses.Validated;
    }

    /// <summary>Whether a transition runs the retirement rules before it applies.</summary>
    /// <param name="from">The current status.</param>
    /// <param name="to">The target status.</param>
    /// <returns>True for published → retired.</returns>
    public static bool RunsRetirementRules(string from, string to)
    {
        return from == PublicationStatuses.Published && to == PublicationStatuses.Retired;
    }
}
