using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// The shape of the three binding rules: every binding of a pinned dataset version that resolves through an identity of one living
/// kind needs that identity to have a published version, because the active assumption set pins it and the engine resolves the
/// binding in it. Each finding belongs to the binding's manifest row and targets the identity.
/// </summary>
/// <param name="reader">Reads the pinned bindings.</param>
public abstract class BindingIdentityRule(ReleaseManifestReader reader) : IReleaseRule
{
    /// <summary>Publication: a draft may pin versions whose identities are still being published.</summary>
    public RuleStage Stage => RuleStage.Publication;

    /// <summary>The rule code.</summary>
    protected abstract string Code { get; }

    /// <summary>The identity's <c>PinKind</c>, the area of the findings' targets.</summary>
    protected abstract string Area { get; }

    /// <summary>Reports every binding whose identity has no published version.</summary>
    /// <param name="snapshot">The release.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per unresolvable binding.</returns>
    public async Task<IReadOnlyList<ReleaseViolation>> EvaluateAsync(ReleaseSnapshot snapshot, CancellationToken cancellationToken)
    {
        var resolving = (await reader.ListBindingsAsync(snapshot, cancellationToken))
            .Select(pinned => (Pinned: pinned, IdentityId: IdentityOf(pinned.Binding)))
            .Where(pair => pair.IdentityId is not null)
            .Select(pair => (pair.Pinned, IdentityId: pair.IdentityId!.Value))
            .ToList();
        long[] identityIds = [.. resolving.Select(pair => pair.IdentityId).Distinct()];
        var published = await PublishedAsync(identityIds, cancellationToken);
        var keys = await KeysAsync(identityIds, cancellationToken);
        return
        [
            .. resolving
                .Where(pair => !published.Contains(pair.IdentityId))
                .Select(pair => new ReleaseViolation
                {
                    Code = Code,
                    Message = Message(pair.Pinned, keys[pair.IdentityId]),
                    DatasetId = pair.Pinned.DatasetId,
                    Target = ReleaseFindings.Target(Area, pair.IdentityId)
                })
        ];
    }

    /// <summary>The identity a binding resolves through; null when the binding needs none of this kind.</summary>
    /// <param name="binding">A pinned binding.</param>
    /// <returns>The identity's id, or null.</returns>
    protected abstract long? IdentityOf(DimensionBindingRecord binding);

    /// <summary>The identities among some that have a published version.</summary>
    /// <param name="identityIds">The identities.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The published ones.</returns>
    protected abstract Task<IReadOnlySet<long>> PublishedAsync(long[] identityIds, CancellationToken cancellationToken);

    /// <summary>The keys of some identities.</summary>
    /// <param name="identityIds">The identities.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Each identity's key by id.</returns>
    protected abstract Task<IReadOnlyDictionary<long, string>> KeysAsync(long[] identityIds, CancellationToken cancellationToken);

    /// <summary>The message of an unresolvable binding.</summary>
    /// <param name="pinned">The binding.</param>
    /// <param name="identityKey">Its identity's key.</param>
    /// <returns>The message.</returns>
    protected abstract string Message(PinnedBinding pinned, string identityKey);
}
