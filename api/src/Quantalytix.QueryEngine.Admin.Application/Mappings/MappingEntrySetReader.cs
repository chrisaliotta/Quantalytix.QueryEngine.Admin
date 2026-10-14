using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>Reads what the entry member rules check: a mapping version's entries and the members of the dimension version it pins.</summary>
public sealed class MappingEntrySetReader(
    ITableGateway<MappingVersionRecord> versions,
    ITableGateway<DimensionMappingRecord> entries,
    ITableGateway<DimensionMemberRecord> members)
{
    /// <summary>Reads a mapping version's entries with its pinned members.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The version's entry set.</returns>
    public async Task<MappingEntrySet> ReadAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { MappingVersionId = versionId }, cancellationToken);
        var rows = await entries.ListAsync(new { MappingVersionId = versionId }, cancellationToken);
        var pinned = await members.ListAsync(new { version.DimensionVersionId }, cancellationToken);
        return new MappingEntrySet
        {
            Entries = [.. rows.OrderBy(row => row.SourceKeyValue, StringComparer.Ordinal)],
            Members = pinned.ToDictionary(member => member.MemberId)
        };
    }
}
