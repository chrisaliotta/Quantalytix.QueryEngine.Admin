namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>The contract's <c>DatasetReleasePin</c>: a semantic release whose manifest pins the version.</summary>
public sealed record DatasetReleasePin
{
    /// <summary>The release's id.</summary>
    public required string ReleaseId { get; init; }

    /// <summary>The release key.</summary>
    public required string ReleaseKey { get; init; }

    /// <summary>Whether the release is the tenant's active one, which queries read.</summary>
    public required bool IsActive { get; init; }
}
