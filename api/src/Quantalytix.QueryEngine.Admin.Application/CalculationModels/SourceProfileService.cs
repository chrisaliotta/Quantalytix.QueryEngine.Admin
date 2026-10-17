using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The source profiles of calculation model versions, each with the source it picks for every variable.</summary>
public sealed class SourceProfileService(
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<SourceProfileVariableRecord> picks,
    ModelContentRemover remover,
    ReportUses reports,
    IIdGenerator ids)
    : IModelVersionContent<SourceProfile, SourceProfileInput>
{
    /// <summary>Lists the version's profiles with their picks.</summary>
    /// <param name="versionId">The model version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The profiles by key.</returns>
    public async Task<IReadOnlyList<SourceProfile>> ListAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await profiles.ListAsync(new { ModelVersionId = versionId }, cancellationToken);
        var picked = (await picks.ListAsync(new { SourceProfileId = rows.Select(row => row.SourceProfileId).ToArray() }, cancellationToken))
            .ToLookup(pick => pick.SourceProfileId);
        return
        [
            .. rows.OrderBy(row => row.ProfileKey, StringComparer.OrdinalIgnoreCase)
                .Select(row => new SourceProfile { Profile = row, Picks = [.. picked[row.SourceProfileId].OrderBy(pick => pick.VariableId)] })
        ];
    }

    /// <summary>Whether a profile belongs to the version.</summary>
    /// <param name="versionId">The model version the route names.</param>
    /// <param name="itemId">The profile the route names.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the profile is one of the version's.</returns>
    public async Task<bool> BelongsAsync(long versionId, long itemId, CancellationToken cancellationToken)
    {
        return (await profiles.ListAsync(new { SourceProfileId = itemId, ModelVersionId = versionId }, cancellationToken)).Count == 1;
    }

    /// <summary>Adds a profile with its picks to a draft version.</summary>
    /// <param name="versionId">The draft model version.</param>
    /// <param name="input">The profile, already validated.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The stored profile.</returns>
    public Task<SourceProfile> CreateAsync(long versionId, SourceProfileInput input, CancellationToken cancellationToken)
    {
        return StoreAsync(ids.NextId(), versionId, input, cancellationToken);
    }

    /// <summary>Replaces a profile and its complete pick list, keeping the profile's ID.</summary>
    /// <param name="versionId">The draft model version.</param>
    /// <param name="itemId">The profile, one of the version's.</param>
    /// <param name="input">The profile's new values, already validated.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The stored profile.</returns>
    public async Task<SourceProfile> UpdateAsync(long versionId, long itemId, SourceProfileInput input, CancellationToken cancellationToken)
    {
        await picks.DeleteAsync(new { SourceProfileId = itemId }, cancellationToken);
        return await StoreAsync(itemId, versionId, input, cancellationToken);
    }

    /// <summary>Deletes a profile with its picks, unless a report version runs under it by default.</summary>
    /// <param name="itemId">The profile.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the profile was deleted; otherwise the detail naming the report versions.</returns>
    public async Task<string?> DeleteAsync(long itemId, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(new { SourceProfileId = itemId }, cancellationToken);
        var blockers = new DeleteBlockers($"Source profile '{profile.ProfileKey}'");
        blockers.AddNamed("the default profile of report versions", await reports.ProfileDefaultsAsync([itemId], cancellationToken));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;
        await remover.RemoveProfilesAsync([itemId], cancellationToken);
        return null;
    }

    /// <summary>Writes a profile row (an upsert) and its picks.</summary>
    private async Task<SourceProfile> StoreAsync(long profileId, long versionId, SourceProfileInput input, CancellationToken cancellationToken)
    {
        var profile = new SourceProfileRecord
        {
            SourceProfileId = profileId,
            ModelVersionId = versionId,
            ProfileKey = input.ProfileKey,
            DisplayName = input.DisplayName,
            IsEnabled = input.IsEnabled
        };
        SourceProfileVariableRecord[] rows =
        [
            .. input.Picks.OrderBy(pick => pick.VariableId).Select(pick => new SourceProfileVariableRecord
            {
                SourceProfileId = profileId, VariableId = pick.VariableId, VariableSourceId = pick.VariableSourceId
            })
        ];
        await profiles.InsertAsync(profile, cancellationToken);
        await picks.InsertManyAsync(rows, cancellationToken);
        return new SourceProfile { Profile = profile, Picks = rows };
    }
}
