namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>CreateDimensionInput</c>: a user, analytical dimension with its live version 1 and first role.</summary>
public sealed record CreateDimensionInput
{
    /// <summary>The dimension key, unique in the tenant and fixed after creation.</summary>
    public required string Key { get; init; }

    /// <summary>The display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>A <c>dimension_kind</c> value, fixed after creation.</summary>
    public required string Kind { get; init; }

    /// <summary>A <c>range_data_type</c> value for a range dimension; null for every other kind.</summary>
    public required string? RangeDataType { get; init; }

    /// <summary>The first role, required for every kind but time, which has none (M6).</summary>
    public required DimensionFirstRoleInput? FirstRole { get; init; }

    /// <summary>Whether the dimension is enabled.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>Whether the dimension may be deleted.</summary>
    public required bool IsRemovable { get; init; }
}
