namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The editable attributes of a dimension role, as <c>PUT dimensions/{dimensionId}/roles/{roleId}</c> takes them.</summary>
public record DimensionRoleInput
{
    /// <summary>Human-readable name the catalog shows, such as <c>Servicing branch</c>; at most 256 bytes of UTF-8 text.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Optional description the catalog shows; at most 1024 bytes of UTF-8 text.</summary>
    public string? Description { get; init; }
}
