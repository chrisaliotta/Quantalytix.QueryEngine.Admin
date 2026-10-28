using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// <c>bound_dimensions_resolvable</c>: the dimension of every binding of every pinned dataset version has a published version. Time
/// bindings are exempt: the engine derives their calendar periods from the bound field.
/// </summary>
public sealed class BoundDimensionsResolvableRule(
    ReleaseManifestReader reader,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<DimensionRecord> dimensions)
    : BindingIdentityRule(reader)
{
    /// <summary><c>bound_dimensions_resolvable</c>.</summary>
    protected override string Code => "bound_dimensions_resolvable";

    /// <summary>A dimension.</summary>
    protected override string Area => PinKinds.Dimension;

    /// <summary>The bound dimension of every binding but a time binding.</summary>
    /// <param name="binding">A pinned binding.</param>
    /// <returns>The dimension, or null for a time binding.</returns>
    protected override long? IdentityOf(DimensionBindingRecord binding)
    {
        return binding.BindingKind == BindingKinds.Time ? null : binding.DimensionId;
    }

    /// <summary>The dimensions with a published version.</summary>
    /// <param name="identityIds">The dimensions.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The published ones.</returns>
    protected override async Task<IReadOnlySet<long>> PublishedAsync(long[] identityIds, CancellationToken cancellationToken)
    {
        return (await dimensionVersions.ListAsync(new { DimensionId = identityIds, Status = PublicationStatuses.Published }, cancellationToken))
            .Select(version => version.DimensionId)
            .ToHashSet();
    }

    /// <summary>The dimensions' keys.</summary>
    /// <param name="identityIds">The dimensions.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Each key by id.</returns>
    protected override async Task<IReadOnlyDictionary<long, string>> KeysAsync(long[] identityIds, CancellationToken cancellationToken)
    {
        return (await dimensions.ListAsync(new { DimensionId = identityIds }, cancellationToken)).ToDictionary(row => row.DimensionId, row => row.DimensionKey);
    }

    /// <summary>Names the binding, its dataset and its dimension.</summary>
    /// <param name="pinned">The binding.</param>
    /// <param name="identityKey">The dimension's key.</param>
    /// <returns>The message.</returns>
    protected override string Message(PinnedBinding pinned, string identityKey)
    {
        return $"Binding '{pinned.Name}' of dataset '{pinned.DatasetKey}' binds dimension '{identityKey}', which has no published version.";
    }
}
