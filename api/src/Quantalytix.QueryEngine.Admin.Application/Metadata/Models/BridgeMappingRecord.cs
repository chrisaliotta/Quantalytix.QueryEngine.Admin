namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>
/// A <c>qe_bridge_mapping</c> row: one source-member to target-member assignment of a bridge version, by member key. The keys
/// resolve in the versions of the bridge's dimensions an assumption set pins.
/// </summary>
public sealed record BridgeMappingRecord
{
    /// <summary>Surrogate primary key; the version and both member keys are unique together.</summary>
    public required long BridgeMappingId { get; init; }

    /// <summary>The owning bridge version.</summary>
    public required long BridgeVersionId { get; init; }

    /// <summary>The key of the source member.</summary>
    public required string SourceMemberKey { get; init; }

    /// <summary>The key of the target member.</summary>
    public required string TargetMemberKey { get; init; }

    /// <summary>Signed business-value coefficient, such as <c>-1</c> for sign normalization.</summary>
    public required decimal Factor { get; init; }

    /// <summary>Nonnegative ownership multiplier; fractional for <c>partition</c> allocation.</summary>
    public required decimal AllocationWeight { get; init; }
}
