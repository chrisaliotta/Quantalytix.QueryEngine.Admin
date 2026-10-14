namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>A mapping entry route: the mapping version and one of its entries.</summary>
public sealed record MappingEntryReference
{
    /// <summary>The mapping version that owns the entry.</summary>
    public required long VersionId { get; init; }

    /// <summary>The entry.</summary>
    public required long EntryId { get; init; }
}
