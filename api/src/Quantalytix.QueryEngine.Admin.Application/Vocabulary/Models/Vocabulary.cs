namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>
/// A controlled vocabulary: the closed set of values a metadata column accepts. Declared as a public static field of type
/// <see cref="Vocabulary"/>, conventionally <c>Definition</c>, anywhere in the Application assembly; <see cref="VocabularyCatalog"/>
/// finds it.
/// </summary>
public sealed record Vocabulary
{
    /// <summary>The schema's vocabulary name, such as <c>publication_status</c>, which validator messages quote.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The vocabulary's key in <c>GET /api/vocabularies</c>, such as <c>versionStatus</c>; null for a vocabulary only
    /// validators read.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>What the vocabulary classifies.</summary>
    public required string Description { get; init; }

    /// <summary>The allowed values, in the schema's order.</summary>
    public required IReadOnlyList<VocabularyValue> Values { get; init; }
}
