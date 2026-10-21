using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>One assignment of a source member to a target member, with its value factor and allocation weight.</summary>
public sealed record BridgeMappingInput
{
    /// <summary>The key of a member of the bridge's source dimension.</summary>
    public required string SourceMemberKey { get; init; }

    /// <summary>The key of a member of the bridge's target dimension.</summary>
    public required string TargetMemberKey { get; init; }

    /// <summary>Signed business-value coefficient, such as <c>-1</c> for sign normalization; fits <c>DECIMAL(38, 12)</c>.</summary>
    public required decimal Factor { get; init; }

    /// <summary>Ownership multiplier of 0 or more; fractional for <c>partition</c> allocation; fits <c>DECIMAL(38, 12)</c>.</summary>
    public required decimal AllocationWeight { get; init; }

    /// <summary>
    /// A <c>source_selection</c> value, <c>member</c> when omitted. Only a report source's bridge maps a member's descendants; its
    /// editor writes them.
    /// </summary>
    public string SourceSelection { get; init; } = SourceSelections.Member;
}
