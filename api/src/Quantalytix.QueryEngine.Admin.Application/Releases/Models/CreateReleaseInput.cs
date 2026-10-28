namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>The contract's <c>CreateReleaseInput</c>: a draft's key and the release whose manifest it starts from.</summary>
public sealed record CreateReleaseInput
{
    /// <summary>The natural key, fixed after creation (R7).</summary>
    public required string Key { get; init; }

    /// <summary>The release whose manifest the draft copies; null starts empty.</summary>
    public required long? CopyFromReleaseId { get; init; }
}
