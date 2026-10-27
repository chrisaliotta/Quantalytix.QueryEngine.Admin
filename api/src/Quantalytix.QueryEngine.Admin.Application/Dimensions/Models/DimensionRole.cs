namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionRole</c>.</summary>
public sealed record DimensionRole
{
    /// <summary>The role's id.</summary>
    public required string Id { get; init; }

    /// <summary>The dimension's id.</summary>
    public required string DimensionId { get; init; }

    /// <summary>The role key, unique in the tenant and fixed after creation.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The description.</summary>
    public required string? Description { get; init; }
}
