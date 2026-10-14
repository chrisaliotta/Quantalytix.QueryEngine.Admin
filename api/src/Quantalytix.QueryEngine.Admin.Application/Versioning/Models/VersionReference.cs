namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>Identifies one versioned object by its kind and surrogate key.</summary>
public sealed record VersionReference
{
    /// <summary>A <c>version_kind</c> value, such as <c>dimension_version</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>The version's surrogate key.</summary>
    public required long Id { get; init; }
}
