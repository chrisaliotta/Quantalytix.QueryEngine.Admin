namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>The contract's <c>DimensionCreated</c>: a new dimension and its live version 1.</summary>
public sealed record DimensionCreated
{
    /// <summary>The new dimension.</summary>
    public required long DimensionId { get; init; }

    /// <summary>Its live version 1.</summary>
    public required long VersionId { get; init; }
}
