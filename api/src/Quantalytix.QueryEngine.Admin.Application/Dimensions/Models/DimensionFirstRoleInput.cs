namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionFirstRoleInput</c>: the role a new dimension starts with.</summary>
public sealed record DimensionFirstRoleInput
{
    /// <summary>The role key, unique in the tenant.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }
}
