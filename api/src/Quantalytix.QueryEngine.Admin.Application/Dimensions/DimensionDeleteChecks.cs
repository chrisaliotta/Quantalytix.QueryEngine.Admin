using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Finds what blocks deleting a dimension, a level or a member: rows of other features that still reference it, and, in a
/// published version, the live objects that name its key. Each check runs before anything is deleted. A draft dimension
/// version has no such blockers, because other objects name keys of the published version only.
/// </summary>
public sealed class DimensionDeleteChecks(
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<RangeBandRecord> bands,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<ReportRecord> reports,
    ITableGateway<CalculationDimensionRecord> calculationDimensions,
    ReportVersionNames reportVersions,
    DimensionConsumers consumers)
{
    /// <summary>
    /// What blocks deleting a dimension: it is not removable, a version is not a draft, or a binding, mapping set, bridge (either
    /// end), report or calculation dimension references it.
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
        blockers.AddNamed("referenced by bindings", boundBy.Select(binding => $"'{binding.DisplayName}'"));
        var sets = await mappingSets.ListAsync(new { dimension.DimensionId }, cancellationToken);
        blockers.AddNamed("referenced by mapping sets", sets.Select(set => $"'{set.MappingSetKey}'"));
        var bridgesFrom = await bridges.ListAsync(new { SourceDimensionId = dimension.DimensionId }, cancellationToken);
        var bridgesTo = await bridges.ListAsync(new { TargetDimensionId = dimension.DimensionId }, cancellationToken);
        blockers.AddNamed("referenced by bridges", bridgesFrom.Concat(bridgesTo).Select(bridge => $"'{bridge.BridgeKey}'"));
        var reportsOn = await reports.ListAsync(new { ReportDimensionId = dimension.DimensionId }, cancellationToken);
        blockers.AddNamed("the report dimension of reports", reportsOn.Select(report => $"'{report.ReportKey}'"));
        var grouping = await calculationDimensions.ListAsync(new { dimension.DimensionId }, cancellationToken);
        var names = await reportVersions.OfIdsAsync(grouping.Select(calculationDimension => calculationDimension.ReportVersionId), cancellationToken);
        blockers.AddNamed("referenced by calculation dimensions", grouping.Select(calculationDimension =>
            $"'{calculationDimension.DimensionKey}' of {names[calculationDimension.ReportVersionId]}"));
        return blockers.Detail();
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
