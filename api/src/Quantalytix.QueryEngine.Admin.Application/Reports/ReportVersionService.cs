using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Reads, creates, clones, edits and deletes the versions of reports. Status changes go through the version workflow.</summary>
public sealed class ReportVersionService(
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportVersionRecord> versions,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<ReportLineRecord> lines,
    ITableGateway<ReleaseReportRecord> pins,
    ITableGateway<SemanticReleaseRecord> releases,
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
    /// Creates a draft with the report's next version number: empty, or with the lines and columns of
    /// <see cref="NewReportVersionInput.CloneFromVersionId"/>, each metric followed by key into the draft's model version.
    /// </summary>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The report dimension, model version, defaults and optional clone source.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new draft and the columns and lines a clone dropped.</returns>
    public async Task<CreatedReportVersion> CreateAsync(long reportId, NewReportVersionInput input, CancellationToken cancellationToken)
    {
        var existing = await versions.ListAsync(new { ReportId = reportId }, cancellationToken);
        var version = new ReportVersionRecord
        {
            ReportVersionId = ids.NextId(),
            ReportId = reportId,
            VersionNumber = VersionNumbers.Next(existing.Select(row => row.VersionNumber)),
            ReportDimensionId = input.ReportDimensionId,
            ModelVersionId = input.ModelVersionId,
            DefaultSourceProfileId = input.DefaultSourceProfileId,
            Status = PublicationStatuses.Draft,
            PayloadJson = input.PayloadJson,
            DefaultFilterJson = input.DefaultFilterJson,
            DefaultSettingsJson = input.DefaultSettingsJson,
            DefaultPivotJson = input.DefaultPivotJson
        };
        await versions.InsertAsync(version, cancellationToken);
        if (input.CloneFromVersionId is not { } sourceVersionId)
            return new CreatedReportVersion { Version = version, DroppedColumns = [], DroppedLines = [] };
        return await cloner.CloneAsync(sourceVersionId, version, cancellationToken);
    }

    /// <summary>Replaces a draft version's default profile, payload and defaults; its lines and columns stay.</summary>
    /// <param name="versionId">The draft report version.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The stored version.</returns>
    public async Task<ReportVersionRecord> UpdateAsync(long versionId, ReportVersionInput input, CancellationToken cancellationToken)
    {
        var updated = await GetAsync(versionId, cancellationToken) with
        {
            DefaultSourceProfileId = input.DefaultSourceProfileId,
            PayloadJson = input.PayloadJson,
            DefaultFilterJson = input.DefaultFilterJson,
            DefaultSettingsJson = input.DefaultSettingsJson,
            DefaultPivotJson = input.DefaultPivotJson
        };
        await versions.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>
    /// Deletes a draft version that no release pins, with its lines and columns, and clears the report's current-version
    /// pointer when it named the version.
    /// </summary>
    /// <param name="versionId">The draft report version.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>Null when the version was deleted; otherwise the detail naming the releases, and nothing was deleted.</returns>
    public async Task<string?> DeleteAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await GetAsync(versionId, cancellationToken);
        var blockers = new DeleteBlockers($"Report version v{version.VersionNumber}");
        blockers.AddNamed("pinned by releases", await ReleaseNamesAsync([versionId], cancellationToken));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;

        var report = await reports.GetAsync(new { version.ReportId }, cancellationToken);
        if (report.CurrentReportVersionId == versionId) await reports.UpdateAsync(report with { CurrentReportVersionId = null }, cancellationToken);
        await RemoveAsync([versionId], cancellationToken);
        return null;
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

    /// <summary>The keys of the releases that pin any of the versions, quoted.</summary>
    /// <param name="versionIds">Report versions.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One name per pinning release.</returns>
    public async Task<IEnumerable<string>> ReleaseNamesAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        var pinning = await pins.ListAsync(new { ReportVersionId = versionIds }, cancellationToken);
        var keys = await releases.ListAsync(new { ReleaseId = pinning.Select(pin => pin.ReleaseId).Distinct().ToArray() }, cancellationToken);
        return keys.Select(release => $"'{release.ReleaseKey}'");
    }
}
