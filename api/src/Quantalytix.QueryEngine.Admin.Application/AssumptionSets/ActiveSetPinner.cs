using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>
/// Repoints the active assumption set to a living kind's version as it is published, so the active set always pins the
/// published versions, the defaults every query that names no set reads.
/// </summary>
/// <param name="kind">The living <c>version_kind</c> the pinner serves.</param>
/// <param name="stores">The version stores, which find the published version's identity.</param>
/// <param name="pins">The assumption set pins.</param>
public sealed class ActiveSetPinner(string kind, VersionStoreRegistry stores, AssumptionPins pins) : IVersionLifecycleHandler
{
    /// <summary>The living kind whose publishes the pinner follows.</summary>
    public string Kind { get; } = kind;

    /// <summary>Validation derives nothing.</summary>
    /// <param name="versionId">The version being validated.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task OnValidatingAsync(long versionId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>Pins the published version in the active set, replacing its identity's previous pin.</summary>
    /// <param name="versionId">The version just published.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>A task that completes when the active set pins the version.</returns>
    public async Task OnPublishedAsync(long versionId, CancellationToken cancellationToken)
    {
        var identityId = await stores.GetLiving(Kind).IdentityIdAsync(versionId, cancellationToken);
        await pins.PinActiveAsync(new PinnedVersion(Kind, identityId, versionId), cancellationToken);
    }
}
