using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The contract's <c>MappingVersionContent</c>: a version, its entries, the dimension's coverage and the rule findings.</summary>
public sealed record MappingVersionContent
{
    /// <summary>The version.</summary>
    public required MappingVersion Version { get; init; }

    /// <summary>The entries, by source value in ordinal order.</summary>
    public required IReadOnlyList<MappingEntry> Entries { get; init; }

    /// <summary>The assignable members of the dimension's live version, and those no entry maps to.</summary>
    public required MappingCoverage Coverage { get; init; }

    /// <summary>The rule findings on this content, with <c>target.itemId</c> naming the entry at fault; empty on a saved live version.</summary>
    public required IReadOnlyList<RuleViolation> Violations { get; init; }
}
