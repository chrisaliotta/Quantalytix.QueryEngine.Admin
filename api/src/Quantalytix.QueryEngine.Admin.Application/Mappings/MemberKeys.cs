using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Finds members by key, which is how mapping entries and bridge mappings move from one dimension version to another:
/// member IDs are global, but a member keeps its key across versions.
/// </summary>
internal static class MemberKeys
{
    /// <summary>
    /// The member ID of each key that exactly one of the members holds, compared exactly. A key that several members of a
    /// draft share identifies none of them, so it is left out.
    /// </summary>
    /// <param name="members">The members of one dimension version.</param>
    /// <returns>The member IDs by key.</returns>
    public static IReadOnlyDictionary<string, long> UniqueIds(IEnumerable<DimensionMemberRecord> members)
    {
        return members
            .GroupBy(member => member.MemberKey, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single().MemberId, StringComparer.Ordinal);
    }
}
