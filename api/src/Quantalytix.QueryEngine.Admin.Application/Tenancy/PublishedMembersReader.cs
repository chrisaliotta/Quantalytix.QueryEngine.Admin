using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>Reads the members of a dimension's live version for pickers that name members by key (bindings, mappings, pins).</summary>
public sealed class PublishedMembersReader(
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members)
{
    /// <summary>Reads the live version's members of a dimension.</summary>
    /// <param name="dimensionId">An existing dimension.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The members in hierarchy order, or null while the dimension has no live version.</returns>
    public async Task<PublishedMembers?> ReadAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var live = (await versions.ListAsync(new { DimensionId = dimensionId, Status = PublicationStatuses.Published }, cancellationToken)).SingleOrDefault();
        if (live is null) return null;

        var owner = new { live.DimensionVersionId };
        var levelKeys = (await levels.ListAsync(owner, cancellationToken)).ToDictionary(level => level.DimensionLevelId, level => level.LevelKey);
        var rows = await members.ListAsync(owner, cancellationToken);
        var keyOf = rows.ToDictionary(member => member.DimensionMemberId, member => member.MemberKey);
        var children = rows
            .OrderBy(member => member.SortOrder)
            .ThenBy(member => member.MemberKey, StringComparer.Ordinal)
            .ToLookup(member => member.ParentDimensionMemberId);

        return new PublishedMembers
        {
            DimensionId = dimensionId,
            VersionId = live.DimensionVersionId,
            Members = [.. InHierarchyOrder(children, null).Select(member => new PublishedMember
            {
                Key = member.MemberKey,
                DisplayName = member.DisplayName,
                ParentKey = member.ParentDimensionMemberId is { } parentId ? keyOf[parentId] : null,
                IsAssignable = member.IsAssignable,
                LevelKey = member.DimensionLevelId is { } levelId ? levelKeys[levelId] : null
            })]
        };
    }

    /// <summary>The members under a parent, each followed by its descendants.</summary>
    private static IEnumerable<DimensionMemberRecord> InHierarchyOrder(ILookup<long?, DimensionMemberRecord> children, long? parentId)
    {
        return children[parentId].SelectMany(member => InHierarchyOrder(children, member.DimensionMemberId).Prepend(member));
    }
}
