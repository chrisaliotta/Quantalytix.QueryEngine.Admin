using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Finds what blocks deleting a dimension, a role, a level or a member: rows of other features that still reference it, and, in
/// a published version, the live objects that name its key. Each check runs before anything is deleted. A draft dimension
/// version has no such blockers, because other objects name keys of the published version only.
/// </summary>
public sealed class DimensionDeleteChecks(
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<RangeBandRecord> bands,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> datasetVersions,
    ITableGateway<ReportRecord> reports,
    ITableGateway<CalculationDimensionRecord> calculationDimensions,
    ITableGateway<ReportSourceRecord> sources,
    ReportNames reportNames,
    DimensionConsumers consumers)
{
    /// <summary>
    /// What blocks deleting a dimension: it is not removable, a version is not a draft, or a binding, mapping set, bridge (either
    /// end) or report references it, or a calculation dimension or report source names one of its roles. The roles themselves
    /// go with the dimension.
    /// </summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="dimensionVersions">Every version of the dimension.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The 409 detail naming the blockers, or null when the dimension can be deleted.</returns>
    public async Task<string?> DimensionAsync(
        DimensionRecord dimension,
        IReadOnlyList<DimensionVersionRecord> dimensionVersions,
        CancellationToken cancellationToken)
    {
        var blockers = new DeleteBlockers($"Dimension '{dimension.DimensionKey}'");
        if (!dimension.IsRemovable) blockers.Add("it is not removable");
        blockers.AddNamed(
            "it has non-draft versions",
            dimensionVersions.Where(version => version.Status != PublicationStatuses.Draft).Select(version => $"v{version.VersionNumber} ({version.Status})"));
        var boundBy = await bindings.ListAsync(new { dimension.DimensionId }, cancellationToken);
        blockers.AddNamed("bound by dataset versions", await DatasetVersionNamesAsync(boundBy, cancellationToken));
        var sets = await mappingSets.ListAsync(new { dimension.DimensionId }, cancellationToken);
        blockers.AddNamed("referenced by mapping sets", sets.Select(set => $"'{set.MappingSetKey}'"));
        var bridgesFrom = await bridges.ListAsync(new { SourceDimensionId = dimension.DimensionId }, cancellationToken);
        var bridgesTo = await bridges.ListAsync(new { TargetDimensionId = dimension.DimensionId }, cancellationToken);
        blockers.AddNamed("referenced by bridges", bridgesFrom.Concat(bridgesTo).Select(bridge => $"'{bridge.BridgeKey}'"));
        var reportsOn = await reports.ListAsync(new { ReportDimensionId = dimension.DimensionId }, cancellationToken);
        blockers.AddNamed("the report dimension of reports", reportsOn.Select(report => $"'{report.ReportKey}'"));
        var roleIds = (await roles.ListAsync(new { dimension.DimensionId }, cancellationToken)).Select(role => role.RoleId).ToArray();
        await AddReportUsesAsync(blockers, roleIds, cancellationToken);
        return blockers.Detail();
    }

    /// <summary>What blocks deleting a role: a binding, calculation dimension or report source names it.</summary>
    /// <param name="role">The role.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The 409 detail naming the blockers, or null when the role can be deleted.</returns>
    public async Task<string?> RoleAsync(DimensionRoleRecord role, CancellationToken cancellationToken)
    {
        var blockers = new DeleteBlockers($"Role '{role.RoleKey}'");
        var boundBy = await bindings.ListAsync(new { role.RoleId }, cancellationToken);
        blockers.AddNamed("bound by dataset versions", await DatasetVersionNamesAsync(boundBy, cancellationToken));
        await AddReportUsesAsync(blockers, [role.RoleId], cancellationToken);
        return blockers.Detail();
    }

    /// <summary>Adds the calculation dimensions that read the roles and the report sources that bridge from them, in reports enabled or not.</summary>
    private async Task AddReportUsesAsync(DeleteBlockers blockers, long[] roleIds, CancellationToken cancellationToken)
    {
        var reading = await calculationDimensions.ListAsync(new { RoleId = roleIds }, cancellationToken);
        var bridging = await sources.ListAsync(new { RoleId = roleIds }, cancellationToken);
        var names = await reportNames.OfIdsAsync(reading.Select(dimension => dimension.ReportId).Concat(bridging.Select(source => source.ReportId)), cancellationToken);
        blockers.AddNamed("read by calculation dimensions", reading.Select(dimension => $"'{dimension.DimensionKey}' of {names[dimension.ReportId]}"));
        blockers.AddNamed("bridged from by report sources", bridging.Select(source => $"'{source.SourceKey}' of {names[source.ReportId]}"));
    }

    /// <summary>Names the dataset versions that hold bindings, like <c>'loans.details' v2</c>.</summary>
    private async Task<IEnumerable<string>> DatasetVersionNamesAsync(IReadOnlyList<DimensionBindingRecord> bound, CancellationToken cancellationToken)
    {
        var holding = (await datasetVersions.ListAsync(new { DatasetVersionId = bound.Select(binding => binding.DatasetVersionId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(version => version.DatasetVersionId);
        var keys = (await datasets.ListAsync(new { DatasetId = holding.Values.Select(version => version.DatasetId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(dataset => dataset.DatasetId, dataset => dataset.DatasetKey);
        return bound.Select(binding => holding[binding.DatasetVersionId]).Select(version => $"'{keys[version.DatasetId]}' v{version.VersionNumber}");
    }

    /// <summary>What blocks deleting a level: members that use it, and in a published version the live objects that name its key.</summary>
    /// <param name="level">The level of a draft or published version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The 409 detail naming the blockers, or null when the level can be deleted.</returns>
    public async Task<string?> LevelAsync(DimensionLevelRecord level, CancellationToken cancellationToken)
    {
        var blockers = new DeleteBlockers($"Level '{level.LevelKey}'");
        var users = await members.ListAsync(new { level.LevelId }, cancellationToken);
        blockers.AddNamed("used by members", users.Select(member => $"'{member.MemberKey}'"));
        blockers.AddNamed("named by", await LiveConsumersAsync(level.DimensionVersionId, isLevel: true, level.LevelKey, cancellationToken));
        return blockers.Detail();
    }

    /// <summary>
    /// What blocks deleting a member: it has children or range bands, and in a published version the live objects that name
    /// its key.
    /// </summary>
    /// <param name="member">The member of a draft or published version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The 409 detail naming the blockers, or null when the member can be deleted.</returns>
    public async Task<string?> MemberAsync(DimensionMemberRecord member, CancellationToken cancellationToken)
    {
        var blockers = new DeleteBlockers($"Member '{member.MemberKey}'");
        var children = await members.ListAsync(new { ParentMemberId = member.MemberId }, cancellationToken);
        blockers.AddNamed("it has child members", children.Select(child => $"'{child.MemberKey}'"));
        var bandsOfMember = await bands.ListAsync(new { member.MemberId }, cancellationToken);
        blockers.AddNamed("used by range bands", bandsOfMember.Select(band => $"#{band.Ordinal}"));
        blockers.AddNamed("named by", await LiveConsumersAsync(member.DimensionVersionId, isLevel: false, member.MemberKey, cancellationToken));
        return blockers.Detail();
    }

    /// <summary>The live objects that name a level or member key of a version: none for a draft, which nothing resolves keys in.</summary>
    private async Task<IEnumerable<string>> LiveConsumersAsync(long dimensionVersionId, bool isLevel, string key, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DimensionVersionId = dimensionVersionId }, cancellationToken);
        if (version.Status != PublicationStatuses.Published) return [];
        var references = await consumers.ListAsync(version.DimensionId, cancellationToken);
        return references.Where(reference => reference.IsLevel == isLevel && reference.Key == key).Select(reference => reference.Consumer).Distinct();
    }
}
