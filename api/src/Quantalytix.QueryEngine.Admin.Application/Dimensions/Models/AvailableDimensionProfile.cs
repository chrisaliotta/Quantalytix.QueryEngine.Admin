namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>AvailableDimensionProfile</c>: a profile with the dimension defined from it, if any.</summary>
public sealed record AvailableDimensionProfile
{
    /// <summary>The profile.</summary>
    public required DimensionProfile Profile { get; init; }

    /// <summary>The dimension defined from the profile; null when it is not defined yet.</summary>
    public required long? DimensionId { get; init; }
}
