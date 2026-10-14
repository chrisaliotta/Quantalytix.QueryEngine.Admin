namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The editable identity attributes of a dimension, as <c>PUT dimensions/{dimensionId}</c> takes them.</summary>
public record DimensionInput
{
    /// <summary>Human-readable name, at most 256 characters.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Administrative availability.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>Whether the dimension may be deleted. A <c>system</c> dimension keeps its stored value, so an update ignores this one.</summary>
    public required bool IsRemovable { get; init; }
}
