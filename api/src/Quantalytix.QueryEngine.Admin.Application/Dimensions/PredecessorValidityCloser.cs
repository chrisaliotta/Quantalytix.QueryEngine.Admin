using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Closes the open-ended published predecessor when a dimension version is published, by setting its <c>valid_to</c> to
/// the new version's <c>valid_from</c>, so the published windows stay disjoint. It is the only change ever made to a
/// published version: a validity correction, after which the workflow advances the semantic revision.
/// </summary>
public sealed class PredecessorValidityCloser(ITableGateway<DimensionVersionRecord> versions) : IVersionLifecycleHandler
{
    /// <summary>The kind whose lifecycle the closer joins: dimension versions.</summary>
    public string Kind { get; } = VersionKinds.DimensionVersion;

    /// <summary>Validation derives nothing here.</summary>
    /// <param name="versionId">The dimension version being validated.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task OnValidatingAsync(long versionId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>Ends every open-ended published version of the dimension that starts before the newly published one.</summary>
    /// <param name="versionId">The newly published dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes when the predecessor is closed.</returns>
    public async Task OnPublishedAsync(long versionId, CancellationToken cancellationToken)
    {
        var published = await versions.GetAsync(new { DimensionVersionId = versionId }, cancellationToken);
        var siblings = await versions.ListAsync(new { published.DimensionId, Status = PublicationStatuses.Published }, cancellationToken);
        var predecessors = siblings.Where(sibling => sibling.DimensionVersionId != versionId && DimensionValidity.IsOpenPredecessor(sibling, published));
        foreach (var predecessor in predecessors) await versions.UpdateAsync(predecessor with { ValidTo = published.ValidFrom }, cancellationToken);
    }
}
