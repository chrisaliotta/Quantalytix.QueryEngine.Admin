namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_assumption_set_bridge</c> row: the version of one bridge an assumption set uses.</summary>
public sealed record AssumptionSetBridgeRecord
{
    /// <summary>The owning assumption set; first part of the primary key.</summary>
    public required long AssumptionSetId { get; init; }

    /// <summary>The bridge; second part of the primary key.</summary>
    public required long BridgeId { get; init; }

    /// <summary>The pinned version: published in the active set, published or retired in a historical set.</summary>
    public required long BridgeVersionId { get; init; }
}
