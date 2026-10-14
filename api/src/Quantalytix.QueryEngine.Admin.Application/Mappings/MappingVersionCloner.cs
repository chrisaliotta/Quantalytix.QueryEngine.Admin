using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Copies a mapping version's entries into a new draft that may pin another dimension version. Each entry follows its
/// member's key to the member with the same key in the draft's dimension version, so a mapping carries over to a new
/// dimension version; an entry whose key has no such member is dropped and reported.
/// </summary>
public sealed class MappingVersionCloner(
    ITableGateway<DimensionMappingRecord> entries,
    ITableGateway<DimensionMemberRecord> members,
    IIdGenerator ids)
{
    /// <summary>Copies the source's entries into the target under new entry IDs, remapped by member key.</summary>
    /// <param name="sourceVersionId">The mapping version to copy.</param>
    /// <param name="target">The new draft, already stored.</param>
    /// <param name="cancellationToken">Cancels the reads and the insert.</param>
    /// <returns>The source keys of the dropped entries, in ordinal order.</returns>
    public async Task<IReadOnlyList<string>> CloneAsync(long sourceVersionId, MappingVersionRecord target, CancellationToken cancellationToken)
    {
        var sourceEntries = await entries.ListAsync(new { MappingVersionId = sourceVersionId }, cancellationToken);
        var sourceMembers = await members.ListAsync(
            new { MemberId = sourceEntries.Select(entry => entry.MemberId).Distinct().ToArray() },
            cancellationToken);
        var memberKeys = sourceMembers.ToDictionary(member => member.MemberId, member => member.MemberKey);
        var targetIds = MemberKeys.UniqueIds(await members.ListAsync(new { target.DimensionVersionId }, cancellationToken));

        var clones = new List<DimensionMappingRecord>();
        var dropped = new List<string>();
        foreach (var entry in sourceEntries.OrderBy(entry => entry.SourceKeyValue, StringComparer.Ordinal))
        {
            if (targetIds.TryGetValue(memberKeys[entry.MemberId], out var memberId))
                clones.Add(entry with { MappingEntryId = ids.NextId(), MappingVersionId = target.MappingVersionId, MemberId = memberId });
            else
                dropped.Add(entry.SourceKeyValue);
        }

        await entries.InsertManyAsync(clones, cancellationToken);
        return dropped;
    }
}
