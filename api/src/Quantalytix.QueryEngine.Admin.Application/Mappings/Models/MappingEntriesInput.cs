namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The complete entry list of a draft mapping version, as <c>PUT mapping-versions/{versionId}/entries</c> takes it.</summary>
public sealed record MappingEntriesInput
{
    /// <summary>The entries; each source key appears once.</summary>
    public required IReadOnlyList<MappingEntryInput> Entries { get; init; }
}
