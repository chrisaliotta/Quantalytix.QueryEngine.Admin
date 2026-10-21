using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// Creates, edits and deletes a report design's sources. A source is a bridge of the report from the dimension its dataset binds
/// through a role into the report dimension, and one variable source per variable the dataset supplies, each classifying through
/// that bridge. A new source's bridge starts with a draft, which the design's next publish publishes; on the published design it
/// is published at once, empty or with the copied mappings, so its row mappings are live edits like the rest. Every profile that
/// picks no source for one of the source's variables picks the source, and a version without profiles gets profile
/// <c>default</c>, which it runs under.
/// </summary>
public sealed class DesignSourceService(
    ReportDesignVersions designs,
    ReportDesignReader reader,
    BridgeService bridges,
    BridgeVersionService bridgeVersions,
    BridgeMappingService bridgeMappings,
    ITableGateway<BridgeVersionRecord> bridgeVersionRows,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<VariableSourceRecord> sourceRows,
    IVersionContent<VariableSourceRecord, VariableSourceInput> variableSources,
    IVersionContent<SourceProfile, SourceProfileInput> profiles,
    ReportVersionService reportVersions,
    ReportContentRemover remover,
    VersionWorkflow workflow)
{
    /// <summary>The key of the profile a version without profiles gets with its first source.</summary>
    public const string DefaultProfileKey = "default";

    /// <summary>Creates a source with its bridge and variable sources.</summary>
    /// <param name="reportVersionId">A draft or published report version.</param>
    /// <param name="input">The new source, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The design afterwards.</returns>
    public async Task<ReportDesign> CreateAsync(long reportVersionId, NewDesignSourceInput input, CancellationToken cancellationToken)
    {
        var scope = await designs.ReadAsync(reportVersionId, cancellationToken);
        var role = await roles.GetAsync(new { input.RoleId }, cancellationToken);
        var bridge = await bridges.CreateAsync(
            new NewBridgeInput
            {
                BridgeKey = DesignSourceKeys.Bridge(scope.Report.ReportKey, input.Key),
                DisplayName = input.Label,
                SourceDimensionId = role.DimensionId,
                TargetDimensionId = scope.Report.ReportDimensionId,
                IsEnabled = true
            },
            cancellationToken);
        var version = await bridgeVersions.CreateAsync(
            bridge.BridgeId,
            new NewBridgeVersionInput { Cardinality = Cardinalities.ManyToOne, AllocationMode = AllocationModes.None },
            cancellationToken);
        if (input.CopyMappingsFromBridgeId is { } templateId)
            await CopyMappingsAsync(templateId, version.BridgeVersionId, scope.Structure.DimensionVersionId, cancellationToken);
        if (scope.Version.Status == PublicationStatuses.Published)
        {
            await workflow.TransitionAsync(VersionKinds.BridgeVersion, version.BridgeVersionId, PublicationStatuses.Validated, cancellationToken);
            await workflow.TransitionAsync(VersionKinds.BridgeVersion, version.BridgeVersionId, PublicationStatuses.Published, cancellationToken);
        }

        await WriteVariablesAsync(scope.Version, input.Key, bridge.BridgeId, input, [], cancellationToken);
        return await reader.ReadAsync(reportVersionId, cancellationToken);
    }

    /// <summary>Edits a source: its label, and its variable sources, adding and removing variables as the input lists them.</summary>
    /// <param name="reportVersionId">A draft or published report version.</param>
    /// <param name="bridgeId">The source's bridge.</param>
    /// <param name="input">The source's new attributes, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The design afterwards.</returns>
    public async Task<ReportDesign> UpdateAsync(long reportVersionId, long bridgeId, DesignSourceInput input, CancellationToken cancellationToken)
    {
        var scope = await designs.ReadAsync(reportVersionId, cancellationToken);
        var bridge = scope.Bridges[bridgeId];
        if (bridge.DisplayName != input.Label)
            await bridges.UpdateAsync(bridgeId, new BridgeInput { DisplayName = input.Label, IsEnabled = bridge.IsEnabled }, cancellationToken);
        var stored = await sourceRows.ListAsync(new { ReportVersionId = reportVersionId, BridgeId = bridgeId }, cancellationToken);
        await WriteVariablesAsync(scope.Version, DesignSourceKeys.Source(scope.Report.ReportKey, bridge.BridgeKey), bridgeId, input, stored, cancellationToken);
        return await reader.ReadAsync(reportVersionId, cancellationToken);
    }

    /// <summary>
    /// Deletes a source's variable sources with the profile picks that name them, and its bridge when the bridge's only versions
    /// are drafts, because the design that created it is the only one that read it.
    /// </summary>
    /// <param name="reportVersionId">A draft or published report version.</param>
    /// <param name="bridgeId">The source's bridge.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>The design afterwards.</returns>
    public async Task<ReportDesign> DeleteAsync(long reportVersionId, long bridgeId, CancellationToken cancellationToken)
    {
        var stored = await sourceRows.ListAsync(new { ReportVersionId = reportVersionId, BridgeId = bridgeId }, cancellationToken);
        await remover.RemoveSourcesAsync([.. stored.Select(source => source.VariableSourceId)], cancellationToken);
        var ownVersions = await bridgeVersionRows.ListAsync(new { BridgeId = bridgeId }, cancellationToken);
        if (ownVersions.All(version => version.Status == PublicationStatuses.Draft))
            await bridges.RemoveAsync(bridgeId, [.. ownVersions.Select(version => version.BridgeVersionId)], cancellationToken);
        return await reader.ReadAsync(reportVersionId, cancellationToken);
    }

    /// <summary>
    /// Writes one variable source per listed variable, keeping the key and availability of a variable's stored source, deletes the
    /// stored sources of variables the input no longer lists, then completes the profiles.
    /// </summary>
    private async Task WriteVariablesAsync(
        ReportVersionRecord version,
        string sourceKey,
        long bridgeId,
        DesignSourceInput input,
        IReadOnlyList<VariableSourceRecord> stored,
        CancellationToken cancellationToken)
    {
        var storedOf = stored.ToDictionary(source => source.VariableKey, StringComparer.Ordinal);
        foreach (var variable in input.Variables)
        {
            var source = new VariableSourceInput
            {
                VariableKey = variable.VariableKey,
                SourceKey = DesignSourceKeys.VariableSource(sourceKey, variable.VariableKey),
                DatasetId = input.DatasetId,
                Classification = new ClassificationInput { Kind = ClassificationKinds.Bridge, RoleId = input.RoleId, BridgeId = bridgeId },
                TimeFieldKey = input.TimeFieldKey,
                MeasureKey = variable.MeasureKey,
                SourceFilterJson = input.SourceFilterJson,
                IsEnabled = true
            };
            if (storedOf.TryGetValue(variable.VariableKey, out var kept))
                await variableSources.UpdateAsync(version.ReportVersionId, kept.VariableSourceId, source with { SourceKey = kept.SourceKey, IsEnabled = kept.IsEnabled }, cancellationToken);
            else
                await variableSources.CreateAsync(version.ReportVersionId, source, cancellationToken);
        }

        var listed = input.Variables.Select(variable => variable.VariableKey).ToHashSet(StringComparer.Ordinal);
        long[] dropped = [.. stored.Where(source => !listed.Contains(source.VariableKey)).Select(source => source.VariableSourceId)];
        if (dropped.Length > 0) await remover.RemoveSourcesAsync(dropped, cancellationToken);
        await CompleteProfilesAsync(version, await sourceRows.ListAsync(new { version.ReportVersionId, BridgeId = bridgeId }, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Gives every profile that picks no source for one of the source's variables the source's variable source, or, when the
    /// version has no profile, creates profile <c>default</c> with the source's picks and makes it the version's default.
    /// </summary>
    private async Task CompleteProfilesAsync(ReportVersionRecord version, IReadOnlyList<VariableSourceRecord> sources, CancellationToken cancellationToken)
    {
        var stored = await profiles.ListAsync(version.ReportVersionId, cancellationToken);
        if (stored.Count == 0)
        {
            var first = new SourceProfileInput { ProfileKey = DefaultProfileKey, DisplayName = "Default", IsEnabled = true, Picks = [.. sources.Select(Pick)] };
            await profiles.CreateAsync(version.ReportVersionId, first, cancellationToken);
            var defaults = new ReportVersionInput
            {
                DefaultProfileKey = DefaultProfileKey,
                SubtotalPosition = version.SubtotalPosition,
                PayloadJson = version.PayloadJson,
                DefaultFilterJson = version.DefaultFilterJson,
                DefaultSettingsJson = version.DefaultSettingsJson,
                DefaultPivotJson = version.DefaultPivotJson
            };
            await reportVersions.UpdateAsync(version.ReportVersionId, defaults, cancellationToken);
            return;
        }

        foreach (var profile in stored)
        {
            var picked = profile.Picks.Select(pick => pick.VariableKey).ToHashSet(StringComparer.Ordinal);
            var missing = sources.Where(source => !picked.Contains(source.VariableKey)).ToList();
            if (missing.Count == 0) continue;
            var completed = new SourceProfileInput
            {
                ProfileKey = profile.Profile.ProfileKey,
                DisplayName = profile.Profile.DisplayName,
                IsEnabled = profile.Profile.IsEnabled,
                Picks =
                [
                    .. profile.Picks.Select(pick => new SourceProfilePick { VariableKey = pick.VariableKey, VariableSourceId = pick.VariableSourceId }),
                    .. missing.Select(Pick)
                ]
            };
            await profiles.UpdateAsync(version.ReportVersionId, profile.Profile.SourceProfileId, completed, cancellationToken);
        }
    }

    /// <summary>
    /// Copies the entries of a bridge's published version that map into members of the report dimension's paired version, each
    /// with its whole weight, into a new bridge version.
    /// </summary>
    private async Task CopyMappingsAsync(long templateBridgeId, long targetVersionId, long structureVersionId, CancellationToken cancellationToken)
    {
        var template = (await bridgeVersionRows.ListAsync(new { BridgeId = templateBridgeId, Status = PublicationStatuses.Published }, cancellationToken)).Single();
        var rowKeys = (await members.ListAsync(new { DimensionVersionId = structureVersionId }, cancellationToken))
            .Select(member => member.MemberKey)
            .ToHashSet(StringComparer.Ordinal);
        var copied = (await bridgeMappings.ListAsync(template.BridgeVersionId, cancellationToken))
            .Where(entry => rowKeys.Contains(entry.TargetMemberKey))
            .Select(entry => BridgeEntries.ToRow(entry.SourceMemberKey, entry.TargetMemberKey, entry.Factor));
        await bridgeMappings.ReplaceAsync(targetVersionId, new BridgeMappingsInput { Mappings = [.. copied] }, cancellationToken);
    }

    /// <summary>The pick of a variable source for its variable.</summary>
    private static SourceProfilePick Pick(VariableSourceRecord source)
    {
        return new SourceProfilePick { VariableKey = source.VariableKey, VariableSourceId = source.VariableSourceId };
    }
}
