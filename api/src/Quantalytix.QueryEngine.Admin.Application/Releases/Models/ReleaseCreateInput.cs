namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>A request to create a draft semantic release, optionally starting from another release's manifest.</summary>
public sealed record ReleaseCreateInput
{
    /// <summary>The stable release label, unique among the tenant's releases, such as <c>development/baseline</c>.</summary>
    public required string ReleaseKey { get; init; }

    /// <summary>The release whose manifest rows the new draft copies; null starts with an empty manifest.</summary>
    public long? CopyFromSemanticReleaseId { get; init; }
}
