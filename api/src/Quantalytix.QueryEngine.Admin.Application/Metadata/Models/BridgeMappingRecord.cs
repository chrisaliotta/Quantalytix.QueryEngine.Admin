namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>A <c>qe_bridge_mapping</c> row: one source-member to target-member assignment of a bridge version.</summary>
public sealed record BridgeMappingRecord
{
    /// <summary>The owning bridge version; first part of the primary key.</summary>
    public required long BridgeVersionId { get; init; }

    /// <summary>The source member; second part of the primary key.</summary>
    public required long SourceMemberId { get; init; }

    /// <summary>The target member; third part of the primary key.</summary>
    public required long TargetMemberId { get; init; }

    /// <summary>Signed business-value coefficient, such as <c>-1</c> for sign normalization.</summary>
    public required decimal Factor { get; init; }

    /// <summary>Nonnegative ownership multiplier; fractional for <c>partition</c> allocation.</summary>
    public required decimal AllocationWeight { get; init; }
}
