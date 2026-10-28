using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// The contract's <c>MappingOperation</c> union, discriminated by <c>op</c> and named after the port's edit methods (OI-01). One
/// change set covers the mapping set's identity and its live version's entries. The derived types are declared in the contract's
/// order, which the conformance check pairs alternatives by.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(MappingSetUpdateOperation), "updateMappingSet")]
[JsonDerivedType(typeof(MappingEntrySaveOperation), "saveEntry")]
[JsonDerivedType(typeof(MappingEntryDeleteOperation), "deleteEntry")]
[JsonDerivedType(typeof(MappingEntriesReplaceOperation), "replaceEntries")]
public abstract record MappingOperation;

/// <summary>An operation on the live version's entries, which it names.</summary>
public abstract record MappingVersionOperation : MappingOperation
{
    /// <summary>The live version; another version refuses the operation.</summary>
    public required string VersionId { get; init; }
}

/// <summary><c>updateMappingSet</c>: changes the identity fields.</summary>
public sealed record MappingSetUpdateOperation : MappingOperation
{
    /// <summary>The fields to change.</summary>
    public required MappingSetPatch Patch { get; init; }
}

/// <summary><c>saveEntry</c>: adds the entry, or replaces the entry with the same id.</summary>
public sealed record MappingEntrySaveOperation : MappingVersionOperation
{
    /// <summary>The entry.</summary>
    public required MappingEntryInput Input { get; init; }
}

/// <summary><c>deleteEntry</c>: removes an entry.</summary>
public sealed record MappingEntryDeleteOperation : MappingVersionOperation
{
    /// <summary>The entry, stored or temporary.</summary>
    public required string EntryId { get; init; }
}

/// <summary><c>replaceEntries</c>: replaces every entry of the version (bulk paste).</summary>
public sealed record MappingEntriesReplaceOperation : MappingVersionOperation
{
    /// <summary>The complete entry list.</summary>
    public required IReadOnlyList<MappingEntryInput> Entries { get; init; }
}
