using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The rows outside a dimension aggregate that its operations, rules and read models consult, read once when the aggregate loads:
/// a change set's operations apply in memory and cannot read.
/// </summary>
public sealed record DimensionSurroundings
{
    /// <summary>What references the dimension, its roles and its member keys.</summary>
    public required IReadOnlyList<DimensionUsageReference> Usage { get; init; }

    /// <summary>The level and member keys live objects name in the dimension.</summary>
    public required IReadOnlyList<KeyReference> NamedKeys { get; init; }

    /// <summary>The role keys of every other dimension of the tenant, ignoring case, which a new role key must avoid.</summary>
    public required IReadOnlySet<string> OtherRoleKeys { get; init; }

    /// <summary>Every version of the dimension as stored.</summary>
    public required IReadOnlyList<DimensionVersionRecord> Versions { get; init; }

    /// <summary>The member count of every version, by version id.</summary>
    public required IReadOnlyDictionary<long, int> MemberCounts { get; init; }
}
