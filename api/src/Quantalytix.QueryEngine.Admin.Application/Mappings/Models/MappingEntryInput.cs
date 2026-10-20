namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>One mapping entry: an actual source value and the canonical member it maps to.</summary>
public sealed record MappingEntryInput
{
    /// <summary>
    /// The actual scalar source value, stored exactly as given: <c>003000</c> and <c>3000</c> are different values. Not empty,
    /// at most 512 bytes of UTF-8 text.
    /// </summary>
    public required string SourceKeyValue { get; init; }

    /// <summary>The key of the member the value maps to, which queries resolve in the dimension version their assumption set pins.</summary>
    public required string MemberKey { get; init; }
}
