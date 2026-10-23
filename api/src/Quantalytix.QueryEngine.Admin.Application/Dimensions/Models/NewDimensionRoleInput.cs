namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// A new dimension role, as <c>POST dimensions/{dimensionId}/roles</c> takes it and a new dimension names its first role: its
/// fixed key and its editable attributes.
/// </summary>
public sealed record NewDimensionRoleInput : DimensionRoleInput
{
    /// <summary>The dimension ID requests use, such as <c>servicingBranch</c>; unique in the tenant and fixed once created.</summary>
    public required string DimensionRoleKey { get; init; }
}
