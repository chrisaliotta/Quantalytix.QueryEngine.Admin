using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// <c>mapped_bindings_resolvable</c>: the mapping set of every mapped binding of a pinned dataset version has a published version,
/// because the engine maps source keys through it.
/// </summary>
public sealed class MappedBindingsResolvableRule(
    ReleaseManifestReader reader,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<MappingSetRecord> mappingSets)
    : BindingIdentityRule(reader)
{
    /// <summary><c>mapped_bindings_resolvable</c>.</summary>
    protected override string Code => "mapped_bindings_resolvable";

    /// <summary>A mapping set.</summary>
    protected override string Area => PinKinds.MappingSet;

    /// <summary>The mapping set of a mapped binding.</summary>
    /// <param name="binding">A pinned binding.</param>
    /// <returns>The mapping set, or null for any other binding.</returns>
    protected override long? IdentityOf(DimensionBindingRecord binding)
    {
        return binding.BindingKind == BindingKinds.Mapped ? binding.MappingSetId : null;
    }

    /// <summary>The mapping sets with a published version.</summary>
    /// <param name="identityIds">The mapping sets.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The published ones.</returns>
    protected override async Task<IReadOnlySet<long>> PublishedAsync(long[] identityIds, CancellationToken cancellationToken)
    {
        return (await mappingVersions.ListAsync(new { MappingSetId = identityIds, Status = PublicationStatuses.Published }, cancellationToken))
            .Select(version => version.MappingSetId)
            .ToHashSet();
    }

    /// <summary>The mapping sets' keys.</summary>
    /// <param name="identityIds">The mapping sets.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Each key by id.</returns>
    protected override async Task<IReadOnlyDictionary<long, string>> KeysAsync(long[] identityIds, CancellationToken cancellationToken)
    {
        return (await mappingSets.ListAsync(new { MappingSetId = identityIds }, cancellationToken)).ToDictionary(row => row.MappingSetId, row => row.MappingSetKey);
    }

    /// <summary>Names the binding, its dataset and its mapping set.</summary>
    /// <param name="pinned">The binding.</param>
    /// <param name="identityKey">The mapping set's key.</param>
    /// <returns>The message.</returns>
    protected override string Message(PinnedBinding pinned, string identityKey)
    {
        return $"Binding '{pinned.Name}' of dataset '{pinned.DatasetKey}' maps through mapping set '{identityKey}', which has no published version.";
    }
}
