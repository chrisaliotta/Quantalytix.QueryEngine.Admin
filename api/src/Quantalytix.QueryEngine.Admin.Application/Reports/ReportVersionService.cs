using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Reads, creates, clones, edits and deletes the versions of reports. Status changes go through the version workflow.</summary>
public sealed class ReportVersionService(
    ITableGateway<ReportVersionRecord> versions,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportLineRecord> lines,
    ReportVersionCloner cloner,
    IIdGenerator ids)
{
    /// <summary>Lists a report's versions.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The versions by version number.</returns>
    public async Task<IReadOnlyList<ReportVersionRecord>> ListAsync(long reportId, CancellationToken cancellationToken)
    {
        var rows = await versions.ListAsync(new { ReportId = reportId }, cancellationToken);
        return [.. rows.OrderBy(row => row.VersionNumber)];
    }

    /// <summary>Reads one report version.</summary>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The version.</returns>
    public Task<ReportVersionRecord> GetAsync(long versionId, CancellationToken cancellationToken)
    {
        return versions.GetAsync(new { ReportVersionId = versionId }, cancellationToken);
    }

    /// <summary>
    /// Creates a draft with the report's next version number: empty, or with copies of the lines and columns of
    /// <see cref="NewReportVersionInput.CloneFromVersionId"/>.
    /// </summary>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The report dimension, model, defaults and optional clone source.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new draft.</returns>
    public async Task<ReportVersionRecord> CreateAsync(long reportId, NewReportVersionInput input, CancellationToken cancellationToken)
    {
        var existing = await versions.ListAsync(new { ReportId = reportId }, cancellationToken);
        var version = new ReportVersionRecord
        {
            ReportVersionId = ids.NextId(),
            ReportId = reportId,
            VersionNumber = VersionNumbers.Next(existing.Select(row => row.VersionNumber)),
            ReportDimensionId = input.ReportDimensionId,
            ModelId = input.ModelId,
            DefaultProfileKey = input.DefaultProfileKey,
            Status = PublicationStatuses.Draft,
            PayloadJson = input.PayloadJson,
            DefaultFilterJson = input.DefaultFilterJson,
            DefaultSettingsJson = input.DefaultSettingsJson,
            DefaultPivotJson = input.DefaultPivotJson
        };
        await versions.InsertAsync(version, cancellationToken);
        if (input.CloneFromVersionId is { } sourceVersionId) await cloner.CloneAsync(sourceVersionId, version, cancellationToken);
        return version;
    }

    /// <summary>Replaces a version's default profile, payload and defaults; its lines and columns stay.</summary>
    /// <param name="versionId">The draft or published report version.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The stored version.</returns>
    public async Task<ReportVersionRecord> UpdateAsync(long versionId, ReportVersionInput input, CancellationToken cancellationToken)
    {
        var updated = await GetAsync(versionId, cancellationToken) with
        {
            DefaultProfileKey = input.DefaultProfileKey,
            PayloadJson = input.PayloadJson,
            DefaultFilterJson = input.DefaultFilterJson,
            DefaultSettingsJson = input.DefaultSettingsJson,
            DefaultPivotJson = input.DefaultPivotJson
        };
        await versions.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>Deletes a draft version with its lines and columns; nothing references a draft.</summary>
    /// <param name="versionId">The draft report version.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the version is gone.</returns>
    public Task DeleteAsync(long versionId, CancellationToken cancellationToken)
    {
        return RemoveAsync([versionId], cancellationToken);
    }

    /// <summary>Hard-deletes draft versions: their lines and columns first, then the versions.</summary>
    /// <param name="versionIds">The draft versions.</param>
    /// <param name="cancellationToken">Cancels the remaining deletes.</param>
    /// <returns>A task that completes when the versions are deleted.</returns>
    public async Task RemoveAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        await lines.DeleteAsync(new { ReportVersionId = versionIds }, cancellationToken);
        await columns.DeleteAsync(new { ReportVersionId = versionIds }, cancellationToken);
        await versions.DeleteAsync(new { ReportVersionId = versionIds }, cancellationToken);
    }
}
