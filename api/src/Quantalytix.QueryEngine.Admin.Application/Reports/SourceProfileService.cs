using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>The source profiles of reports, each with the source it picks for every variable.</summary>
public sealed class SourceProfileService(
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<SourceProfileVariableRecord> picks,
    ITableGateway<ReportRecord> reports,
    ReportContentRemover remover,
    IIdGenerator ids)
    : IReportContent<SourceProfile, SourceProfileInput>
{
    /// <summary>Lists the report's profiles with their picks.</summary>
    /// <param name="ownerId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The profiles by key.</returns>
    public async Task<IReadOnlyList<SourceProfile>> ListAsync(long ownerId, CancellationToken cancellationToken)
    {
        var rows = await profiles.ListAsync(new { ReportId = ownerId }, cancellationToken);
        var picked = (await picks.ListAsync(new { SourceProfileId = rows.Select(row => row.SourceProfileId).ToArray() }, cancellationToken))
            .ToLookup(pick => pick.SourceProfileId);
        return
        [
            .. rows.OrderBy(row => row.ProfileKey, StringComparer.OrdinalIgnoreCase)
                .Select(row => new SourceProfile { Profile = row, Picks = [.. picked[row.SourceProfileId].OrderBy(pick => pick.VariableKey, StringComparer.Ordinal)] })
        ];
    }

    /// <summary>Whether a profile belongs to the report.</summary>
    /// <param name="ownerId">The report the route names.</param>
    /// <param name="itemId">The profile the route names.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the profile is one of the report's.</returns>
    public async Task<bool> BelongsAsync(long ownerId, long itemId, CancellationToken cancellationToken)
    {
        return (await profiles.ListAsync(new { SourceProfileId = itemId, ReportId = ownerId }, cancellationToken)).Count == 1;
    }

    /// <summary>The profile and picks an input describes, under a new ID or the edited profile's own.</summary>
    /// <param name="ownerId">The report.</param>
    /// <param name="itemId">The edited profile; null for a new profile.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The profile to store.</returns>
    public SourceProfile Compose(long ownerId, long? itemId, SourceProfileInput input)
    {
        var profileId = itemId ?? ids.NextId();
        return new SourceProfile
        {
            Profile = new SourceProfileRecord
            {
                SourceProfileId = profileId,
                ReportId = ownerId,
                ProfileKey = input.ProfileKey,
                DisplayName = input.DisplayName,
                IsEnabled = input.IsEnabled
            },
            Picks =
            [
                .. input.Picks.OrderBy(pick => pick.VariableKey, StringComparer.Ordinal).Select(pick => new SourceProfileVariableRecord
                {
                    SourceProfileId = profileId, VariableKey = pick.VariableKey, ReportSourceId = pick.ReportSourceId
                })
            ]
        };
    }

    /// <summary>Stores a new profile with its picks.</summary>
    /// <param name="item">The composed profile.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>A task that completes when the profile is stored.</returns>
    public async Task InsertAsync(SourceProfile item, CancellationToken cancellationToken)
    {
        await profiles.InsertAsync(item.Profile, cancellationToken);
        await picks.InsertManyAsync(item.Picks, cancellationToken);
    }

    /// <summary>Replaces a profile and its complete pick list, keeping the profile's ID.</summary>
    /// <param name="item">The composed profile.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>A task that completes when the profile is stored.</returns>
    public async Task UpdateAsync(SourceProfile item, CancellationToken cancellationToken)
    {
        await picks.DeleteAsync(new { item.Profile.SourceProfileId }, cancellationToken);
        await InsertAsync(item, cancellationToken);
    }

    /// <summary>Deletes a profile with its picks, unless its report runs under it by default.</summary>
    /// <param name="itemId">The profile.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the profile was deleted; otherwise the detail naming the default it is.</returns>
    public async Task<string?> DeleteAsync(long itemId, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(new { SourceProfileId = itemId }, cancellationToken);
        var report = await reports.GetAsync(new { profile.ReportId }, cancellationToken);
        var blockers = new DeleteBlockers($"Source profile '{profile.ProfileKey}'");
        if (report.DefaultProfileKey == profile.ProfileKey) blockers.Add("it is the default profile of its report");
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;
        await remover.RemoveProfilesAsync([itemId], cancellationToken);
        return null;
    }

    /// <summary>The report with a profile added or replaced, picks and all.</summary>
    /// <param name="report">The report as it stands.</param>
    /// <param name="item">The composed profile.</param>
    /// <returns>The report as the write leaves it.</returns>
    public ReportGraph Put(ReportGraph report, SourceProfile item)
    {
        var without = Without(report, item.Profile.SourceProfileId);
        return without with { Profiles = [.. without.Profiles, item.Profile], Picks = [.. without.Picks, .. item.Picks] };
    }

    /// <summary>The report without a profile and its picks.</summary>
    /// <param name="report">The report as it stands.</param>
    /// <param name="itemId">The profile.</param>
    /// <returns>The report as the delete leaves it.</returns>
    public ReportGraph Without(ReportGraph report, long itemId)
    {
        return report with
        {
            Profiles = [.. report.Profiles.Where(profile => profile.SourceProfileId != itemId)],
            Picks = [.. report.Picks.Where(pick => pick.SourceProfileId != itemId)]
        };
    }
}
