using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// Replaces the variables a report source supplies, each a dataset measure with an optional filter. A variable the source no longer
/// supplies leaves the profiles that picked the source for it. Every profile that picks no source for one of the source's variables
/// picks the source, and a report without profiles gets profile <c>default</c>, which it runs under.
/// </summary>
public sealed class ReportSourceVariableService(
    ITableGateway<ReportRecord> reports,
    ITableGateway<ReportSourceVariableRecord> sourceVariables,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<SourceProfileVariableRecord> picks,
    ReportChanges changes,
    IIdGenerator ids)
{
    /// <summary>The key of the profile a report without profiles gets with its first variables.</summary>
    public const string DefaultProfileKey = "default";

    /// <summary>Replaces a source's variables and completes the report's profiles.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="sourceId">The source, one of the report's.</param>
    /// <param name="input">The source's complete, validated variable list.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The violations that kept an enabled report from taking the list, in which case nothing was written.</returns>
    public async Task<IReadOnlyList<RuleViolation>> ReplaceAsync(long reportId, long sourceId, SourceVariablesInput input, CancellationToken cancellationToken)
    {
        var report = await reports.GetAsync(new { ReportId = reportId }, cancellationToken);
        ReportSourceVariableRecord[] supplied =
        [
            .. input.Variables.OrderBy(variable => variable.VariableKey, StringComparer.Ordinal).Select(variable => new ReportSourceVariableRecord
            {
                ReportSourceId = sourceId,
                VariableKey = variable.VariableKey,
                MeasureKey = variable.MeasureKey,
                SourceFilterJson = variable.SourceFilterJson
            })
        ];
        var listed = supplied.Select(variable => variable.VariableKey).ToHashSet(StringComparer.Ordinal);
        var reportProfiles = await profiles.ListAsync(new { ReportId = reportId }, cancellationToken);
        var storedPicks = await picks.ListAsync(new { SourceProfileId = reportProfiles.Select(profile => profile.SourceProfileId).ToArray() }, cancellationToken);
        var dropped = storedPicks.Where(pick => pick.ReportSourceId == sourceId && !listed.Contains(pick.VariableKey)).ToList();
        var picked = storedPicks.Except(dropped).Select(pick => (pick.SourceProfileId, pick.VariableKey)).ToHashSet();
        SourceProfileRecord? created = reportProfiles.Count == 0
            ? new SourceProfileRecord { SourceProfileId = ids.NextId(), ReportId = reportId, ProfileKey = DefaultProfileKey, DisplayName = "Default", IsEnabled = true }
            : null;
        SourceProfileVariableRecord[] added =
        [
            .. (created is null ? reportProfiles : [created]).SelectMany(profile => supplied
                .Where(variable => !picked.Contains((profile.SourceProfileId, variable.VariableKey)))
                .Select(variable => new SourceProfileVariableRecord { SourceProfileId = profile.SourceProfileId, VariableKey = variable.VariableKey, ReportSourceId = sourceId }))
        ];
        var defaulted = created is null ? report : report with { DefaultProfileKey = created.ProfileKey };

        return await changes.ApplyAsync(
            reportId,
            graph => graph with
            {
                Report = defaulted,
                SourceVariables = [.. graph.SourceVariables.Where(variable => variable.ReportSourceId != sourceId), .. supplied],
                Profiles = created is null ? graph.Profiles : [.. graph.Profiles, created],
                Picks = [.. graph.Picks.Except(dropped), .. added]
            },
            async () =>
            {
                await sourceVariables.DeleteAsync(new { ReportSourceId = sourceId }, cancellationToken);
                await sourceVariables.InsertManyAsync(supplied, cancellationToken);
                await picks.DeleteAsync(new { ReportSourceId = sourceId, VariableKey = dropped.Select(pick => pick.VariableKey).Distinct().ToArray() }, cancellationToken);
                if (created is not null)
                {
                    await profiles.InsertAsync(created, cancellationToken);
                    await reports.UpdateAsync(defaulted, cancellationToken);
                }
                await picks.InsertManyAsync(added, cancellationToken);
            },
            cancellationToken);
    }
}
