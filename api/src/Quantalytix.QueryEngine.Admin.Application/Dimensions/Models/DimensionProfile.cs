using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The contract's <c>DimensionProfile</c>: a system dimension type of the code registry (<see cref="DimensionProfiles"/>, OI-16).
/// A system dimension names its profile in <c>qe_dimension.dimension_profile_key</c>; a user dimension has none.
/// </summary>
public sealed record DimensionProfile
{
    /// <summary>The profile key.</summary>
    public required string Key { get; init; }

    /// <summary>The display name, which a defined dimension takes.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>dimension_kind</c> value.</summary>
    public required string Kind { get; init; }

    /// <summary>The product module that owns the profile.</summary>
    public required string Owner { get; init; }

    /// <summary>A <see cref="DimensionEditPolicies"/> value.</summary>
    public required string EditPolicy { get; init; }

    /// <summary><c>manager</c>, <c>module</c> or <c>hidden</c>: where the dimension is shown.</summary>
    public required string Visibility { get; init; }

    /// <summary>The roles the dimension must keep.</summary>
    public required IReadOnlyList<string> RequiredRoleKeys { get; init; }

    /// <summary>The members the live version must keep.</summary>
    public required IReadOnlyList<string> RequiredMemberKeys { get; init; }

    /// <summary>The member properties the server validates.</summary>
    public required IReadOnlyList<DimensionPropertyField> MemberPropertySchema { get; init; }

    /// <summary><c>one</c> or <c>many</c> dimensions per tenant.</summary>
    public required string Cardinality { get; init; }

    /// <summary>Display names of required members and roles, used when the profile is defined.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? SeedNames { get; init; }

    /// <summary>Whether a second role is refused unconditionally (OI-20).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SingleRole { get; init; }
}
