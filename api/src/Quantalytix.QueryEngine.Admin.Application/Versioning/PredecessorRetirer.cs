using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Retires the version a living kind's publish replaces: the identity's other published version. A living version is never
/// retired by hand, so this is how it retires; it keeps its publication time and never changes again.
/// </summary>
/// <param name="kind">The living <c>version_kind</c> the retirer serves.</param>
/// <param name="stores">The version stores.</param>
public sealed class PredecessorRetirer(string kind, VersionStoreRegistry stores) : IVersionLifecycleHandler
{
    /// <summary>The living kind whose publishes the retirer follows.</summary>
    public string Kind { get; } = kind;

    /// <summary>Validation derives nothing.</summary>
    /// <param name="versionId">The version being validated.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task OnValidatingAsync(long versionId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>Retires the identity's other published versions.</summary>
    /// <param name="versionId">The version just published.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes when the predecessor is retired.</returns>
    public async Task OnPublishedAsync(long versionId, CancellationToken cancellationToken)
    {
        var store = stores.GetLiving(Kind);
        foreach (var siblingId in await store.PublishedSiblingIdsAsync(versionId, cancellationToken))
        {
            var sibling = await store.GetAsync(siblingId, cancellationToken);
            await store.SetStatusAsync(siblingId, PublicationStatuses.Retired, sibling.PublishedAt, cancellationToken);
        }
    }
}
