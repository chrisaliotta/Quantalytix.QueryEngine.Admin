namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>A request to move a version to another lifecycle status.</summary>
public sealed record StatusChangeInput
{
    /// <summary>The target status: a <c>publication_status</c> value.</summary>
    public required string Status { get; init; }
}
