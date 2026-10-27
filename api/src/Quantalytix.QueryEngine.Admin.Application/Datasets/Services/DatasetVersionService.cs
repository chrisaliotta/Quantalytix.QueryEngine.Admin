using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Lists, creates, edits and deletes the versions of datasets. Status changes go through the version workflow.</summary>
public sealed class DatasetVersionService(
    ITableGateway<DatasetVersionRecord> versions,
    ITableGateway<ReleaseDatasetRecord> releaseDatasets,
    ITableGateway<SemanticReleaseRecord> releases,
    DatasetVersionContentStore content,
    DatasetVersionCloner cloner,
    IIdGenerator ids,
    TimeProvider time)
{
    /// <summary>The expression grammar version of a new empty version: the one the query engine parses.</summary>
    private const string ExpressionLanguageVersion = "1.0";

    /// <summary>Lists the versions of a dataset.</summary>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The versions, by version number.</returns>
    public async Task<IReadOnlyList<DatasetVersionRecord>> ListAsync(long datasetId, CancellationToken cancellationToken)
    {
        var rows = await versions.ListAsync(new { DatasetId = datasetId }, cancellationToken);
        return [.. rows.OrderBy(row => row.VersionNumber)];
    }

    /// <summary>Reads one dataset version.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The version.</returns>
    public Task<DatasetVersionRecord> GetAsync(long versionId, CancellationToken cancellationToken)
    {
        return versions.GetAsync(new { DatasetVersionId = versionId }, cancellationToken);
    }

    /// <summary>
    /// Creates a draft with the dataset's next version number: a clone of another version, or an empty version over a source
    /// object that supports aggregate and detail queries.
    /// </summary>
    /// <param name="datasetId">The dataset that owns the new draft.</param>
    /// <param name="input">The clone source, or the source object of an empty draft.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new draft.</returns>
    public async Task<DatasetVersionRecord> CreateAsync(long datasetId, DatasetVersionInput input, CancellationToken cancellationToken)
    {
        var existing = await versions.ListAsync(new { DatasetId = datasetId }, cancellationToken);
        var versionNumber = VersionNumbers.Next(existing.Select(row => row.VersionNumber));
        if (input.CloneFromVersionId is { } sourceVersionId)
            return await cloner.CloneAsync(sourceVersionId, datasetId, versionNumber, cancellationToken);

        var version = new DatasetVersionRecord
        {
            DatasetVersionId = ids.NextId(),
            DatasetId = datasetId,
            VersionNumber = versionNumber,
            Status = PublicationStatuses.Draft,
            SourceObjectKind = input.SourceObjectKind!,
            SourceObject = input.SourceObject!,
            SupportsAggregate = true,
            SupportsDetail = true,
            ExpressionLanguageVersion = ExpressionLanguageVersion,
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        await versions.InsertAsync(version, cancellationToken);
        return version;
    }

    /// <summary>Replaces the settings of a draft version.</summary>
    /// <param name="versionId">The draft version.</param>
    /// <param name="input">The new settings.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The updated version.</returns>
    public async Task<DatasetVersionRecord> UpdateAsync(long versionId, DatasetVersionSettingsInput input, CancellationToken cancellationToken)
    {
        var current = await GetAsync(versionId, cancellationToken);
        var updated = current with
        {
            SourceObjectKind = input.SourceObjectKind,
            SourceObject = input.SourceObject,
            SupportsAggregate = input.SupportsAggregate,
            SupportsDetail = input.SupportsDetail,
            DefaultTimeBindingId = input.DefaultTimeBindingId,
            ExpressionLanguageVersion = input.ExpressionLanguageVersion,
            SecurityPolicyKey = input.SecurityPolicyKey,
            Properties = input.Properties
        };
        await versions.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>
    /// Deletes a draft version with all its content, unless a release manifest pins it: the manifest row would then name a
    /// version that no longer exists.
    /// </summary>
    /// <param name="versionId">The draft version.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the version was deleted; otherwise the detail naming the releases that pin it.</returns>
    public async Task<string?> DeleteDraftAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await GetAsync(versionId, cancellationToken);
        var pins = await releaseDatasets.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var pinning = await releases.ListAsync(new { SemanticReleaseId = pins.Select(pin => pin.SemanticReleaseId).ToArray() }, cancellationToken);
        var blockers = new DeleteBlockers($"Dataset version v{version.VersionNumber}");
        blockers.AddNamed(DeleteBlockerCodes.ReferencedByRelease, "pinned by releases", pinning.Select(release => $"'{release.ReleaseKey}'"));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;

        await DeleteAsync([versionId], cancellationToken);
        return null;
    }

    /// <summary>Deletes draft versions with all their content, children first.</summary>
    /// <param name="versionIds">The draft versions.</param>
    /// <param name="cancellationToken">Cancels the remaining deletes.</param>
    /// <returns>A task that completes when the versions are deleted.</returns>
    public async Task DeleteAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        await content.DeleteAsync(versionIds, cancellationToken);
        await versions.DeleteAsync(new { DatasetVersionId = versionIds }, cancellationToken);
    }
}
