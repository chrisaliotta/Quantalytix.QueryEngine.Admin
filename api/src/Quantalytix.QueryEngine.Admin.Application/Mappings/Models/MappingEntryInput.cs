namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The contract's <c>MappingEntryInput</c>: one entry of the live version, named by its id.</summary>
public sealed record MappingEntryInput
{
    /// <summary>The entry's stored id, or a client-generated <c>tmp:&lt;uuid&gt;</c> for a new entry.</summary>
    public required string Id { get; init; }

    /// <summary>The source value, stored exactly as given: <c>003000</c> and <c>3000</c> are different values (B2).</summary>
    public required string SourceKeyValue { get; init; }

    /// <summary>The key of the member the value maps to.</summary>
    public required string MemberKey { get; init; }
}
