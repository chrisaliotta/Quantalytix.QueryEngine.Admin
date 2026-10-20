namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>One pin of an assumption set as the client shows it: the identity's key and the pinned version's number and status.</summary>
public sealed record AssumptionPin
{
    /// <summary>A living <c>version_kind</c> value.</summary>
    public required string Kind { get; init; }

    /// <summary>The identity, such as a dimension.</summary>
    public required long IdentityId { get; init; }

    /// <summary>The identity's key, such as its <c>dimension_key</c>.</summary>
    public required string IdentityKey { get; init; }

    /// <summary>The pinned version.</summary>
    public required long VersionId { get; init; }

    /// <summary>The pinned version's number.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>The pinned version's <c>publication_status</c>: published, or retired in a historical set.</summary>
    public required string Status { get; init; }
}
