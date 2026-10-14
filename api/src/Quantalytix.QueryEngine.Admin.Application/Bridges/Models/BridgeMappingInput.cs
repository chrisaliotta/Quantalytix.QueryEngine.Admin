namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>One assignment of a source member to a target member, with its value factor and allocation weight.</summary>
public sealed record BridgeMappingInput
{
    /// <summary>A member of the bridge version's source dimension version.</summary>
    public required long SourceMemberId { get; init; }

    /// <summary>A member of the bridge version's target dimension version.</summary>
    public required long TargetMemberId { get; init; }

    /// <summary>Signed business-value coefficient, such as <c>-1</c> for sign normalization; fits <c>DECIMAL(38, 12)</c>.</summary>
    public required decimal Factor { get; init; }

    /// <summary>Ownership multiplier of 0 or more; fractional for <c>partition</c> allocation; fits <c>DECIMAL(38, 12)</c>.</summary>
    public required decimal AllocationWeight { get; init; }
}
