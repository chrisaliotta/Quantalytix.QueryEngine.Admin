using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>Reads a mapping set aggregate as stored: its live version for a change set, or any version for a check.</summary>
public sealed class MappingSetSnapshotLoader(
    ITableGateway<MappingSetRecord> sets,
    ITableGateway<MappingVersionRecord> versions,
    ITableGateway<DimensionMappingRecord> entries,
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<AssumptionSetMappingRecord> pins,
    PublishedDimensionReader published,
    MappingReferenceReader references)
{
    /// <summary>Reads a mapping set with its live version, the one every mapping set has (OI-27).</summary>
    /// <param name="mappingSetId">The mapping set.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public async Task<MappingSetSnapshot> LoadLiveAsync(long mappingSetId, CancellationToken cancellationToken)
    {
        var set = await sets.GetAsync(new { MappingSetId = mappingSetId }, cancellationToken);
        var all = await versions.ListAsync(new { MappingSetId = mappingSetId }, cancellationToken);
        return await LoadAsync(set, all.Single(version => version.Status == PublicationStatuses.Published), all, cancellationToken);
    }

    /// <summary>Reads the mapping set of a version with that version's entries.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The snapshot.</returns>
    public async Task<MappingSetSnapshot> LoadVersionAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { MappingVersionId = versionId }, cancellationToken);
        var set = await sets.GetAsync(new { version.MappingSetId }, cancellationToken);
        var all = await versions.ListAsync(new { version.MappingSetId }, cancellationToken);
        return await LoadAsync(set, version, all, cancellationToken);
    }

    /// <summary>Reads the version's entries and what surrounds the mapping set.</summary>
    private async Task<MappingSetSnapshot> LoadAsync(
        MappingSetRecord set,
        MappingVersionRecord version,
        IReadOnlyList<MappingVersionRecord> all,
        CancellationToken cancellationToken)
    {
        var allEntries = await entries.ListAsync(new { MappingVersionId = all.Select(row => row.MappingVersionId).ToArray() }, cancellationToken);
        var pinned = await pins.ListAsync(new { set.MappingSetId }, cancellationToken);
        var bindings = await references.BindingsAsync(new { set.MappingSetId }, binding => binding.MappingSetId!.Value, cancellationToken);
        return new MappingSetSnapshot
        {
            Set = set,
            Version = version,
            Entries = [.. allEntries.Where(entry => entry.MappingVersionId == version.MappingVersionId).OrderBy(entry => entry.SourceKeyValue, StringComparer.Ordinal)],
            Surroundings = new MappingSetSurroundings
            {
                Dimension = await dimensions.GetAsync(new { set.DimensionId }, cancellationToken),
                Published = await published.ReadAsync(set.DimensionId, cancellationToken),
                Versions = [.. all.OrderBy(row => row.VersionNumber)],
                EntryCounts = all.ToDictionary(row => row.MappingVersionId, row => allEntries.Count(entry => entry.MappingVersionId == row.MappingVersionId)),
                Bindings = [.. bindings[set.MappingSetId]],
                Pins = await references.PinsAsync(
                    [.. pinned.Select(pin => (pin.AssumptionSetId, pin.MappingVersionId))],
                    all.ToDictionary(row => row.MappingVersionId, row => row.VersionNumber),
                    cancellationToken)
            }
        };
    }
}
