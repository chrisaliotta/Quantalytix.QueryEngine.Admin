using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// Lists the items of a bridge source that no rule reaches. A dataset row whose item no rule reaches classifies into no member of
/// the report dimension, so the report leaves it out without a trace; this list is where it shows. A rule reaches its item, and a
/// rule that takes every item under a member reaches the items under it too.
/// </summary>
public sealed class SourceCoverageReader(
    ITableGateway<ReportSourceRecord> sources,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<BridgeMappingRecord> bridgeMappings,
    OwnedVersions owned,
    PublishedDimensionReader publishedDimensions)
{
    /// <summary>The assignable members of a bridge source's item dimension that no rule of its bridge reaches.</summary>
    /// <param name="sourceId">A bridge source.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The unmapped item keys, in ordinal order.</returns>
    public async Task<IReadOnlyList<string>> UnmappedAsync(long sourceId, CancellationToken cancellationToken)
    {
        var source = await sources.GetAsync(new { ReportSourceId = sourceId }, cancellationToken);
        var role = await roles.GetAsync(new { DimensionRoleId = source.DimensionRoleId!.Value }, cancellationToken);
        var bridgeVersion = (await owned.BridgesAsync([source], cancellationToken))[sourceId];
        var entries = await bridgeMappings.ListAsync(new { bridgeVersion.BridgeVersionId }, cancellationToken);
        return Unmapped((await publishedDimensions.ReadAsync(role.DimensionId, cancellationToken))!, entries);
    }

    /// <summary>The assignable items that no entry reaches, by itself or through a member above it whose descendants it takes.</summary>
    /// <param name="items">The published version of the item dimension.</param>
    /// <param name="entries">The entries of the source's bridge.</param>
    /// <returns>The unmapped item keys, in ordinal order.</returns>
    public static IReadOnlyList<string> Unmapped(DimensionKeys items, IEnumerable<BridgeMappingRecord> entries)
    {
        var rules = entries.ToLookup(entry => entry.SourceSelection, entry => entry.SourceMemberKey);
        var members = rules[SourceSelections.Member].ToHashSet(StringComparer.Ordinal);
        var folders = rules[SourceSelections.Descendants].ToHashSet(StringComparer.Ordinal);
        return
        [
            .. items.MemberKeys
                .Where(key => !items.UnassignableMemberKeys.Contains(key) && !members.Contains(key) && !items.SelfAndAncestors(key).Any(folders.Contains))
                .Order(StringComparer.Ordinal)
        ];
    }
}
