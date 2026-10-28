namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The contract's <c>MappingCoverage</c>: assignable members of the dimension's live version and those no entry maps to.</summary>
public sealed record MappingCoverage
{
    /// <summary>The assignable members of the live version.</summary>
    public required int AssignableMemberCount { get; init; }

    /// <summary>The keys of the assignable members no entry maps to, in ordinal order.</summary>
    public required IReadOnlyList<string> UncoveredMemberKeys { get; init; }
}
