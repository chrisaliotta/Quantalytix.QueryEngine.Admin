namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>One mapping entry: an actual source value and the canonical member it maps to.</summary>
public sealed record MappingEntryInput
{
    /// <summary>
    /// The actual scalar source value, stored exactly as given: <c>003000</c> and <c>3000</c> are different values. Not empty,
    /// at most 512 characters.
    /// </summary>
    public required string SourceKeyValue { get; init; }

    /// <summary>The member of the mapping version's dimension version that the value maps to.</summary>
    public required long MemberId { get; init; }
}
