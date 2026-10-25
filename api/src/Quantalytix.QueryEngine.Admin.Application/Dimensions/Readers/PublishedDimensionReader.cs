using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Reads the keys of dimension versions, above all of a dimension's published version: the active assumption set pins it, so
/// bindings, mapping entries, bridge mappings and reports resolve the keys they name in it. While a dimension version's own rules
/// run, it counts as its dimension's published version.
/// </summary>
public sealed class PublishedDimensionReader(
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members,
    PublicationSet publicationSet)
{
    /// <summary>Reads the keys of a dimension's published version, or of the version that counts as published.</summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The published version's keys, or null while the dimension has no published version.</returns>
    public async Task<DimensionKeys?> ReadAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var published = publicationSet.CountingAsPublished(
            VersionKinds.DimensionVersion,
            await versions.ListAsync(new { DimensionId = dimensionId }, cancellationToken),
            version => version.DimensionId,
            version => version.DimensionVersionId,
            version => version.Status);
        return published.Count == 0 ? null : await KeysAsync(published[0], cancellationToken);
    }

    /// <summary>Reads the keys of a dimension version.</summary>
    /// <param name="version">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The version's keys.</returns>
    public async Task<DimensionKeys> KeysAsync(DimensionVersionRecord version, CancellationToken cancellationToken)
    {
        var owner = new { version.DimensionVersionId };
        var versionMembers = await members.ListAsync(owner, cancellationToken);
        var keyOf = versionMembers.ToDictionary(member => member.DimensionMemberId, member => member.MemberKey);
        return new DimensionKeys
        {
            Version = version,
            LevelKeys = (await levels.ListAsync(owner, cancellationToken)).Select(level => level.LevelKey).ToHashSet(StringComparer.Ordinal),
            MemberKeys = versionMembers.Select(member => member.MemberKey).ToHashSet(StringComparer.Ordinal),
            UnassignableMemberKeys = versionMembers.Where(member => !member.IsAssignable).Select(member => member.MemberKey).ToHashSet(StringComparer.Ordinal),
            RootCount = versionMembers.Count(member => member.ParentDimensionMemberId is null),
            ParentKeys = versionMembers
                .Where(member => member.ParentDimensionMemberId is not null)
                .ToDictionary(member => member.MemberKey, member => keyOf[member.ParentDimensionMemberId!.Value], StringComparer.Ordinal)
        };
    }
}
