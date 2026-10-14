namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>A controlled vocabulary: the closed set of values a metadata column accepts.</summary>
public sealed record Vocabulary
{
    /// <summary>The vocabulary key the web client looks values up by, such as <c>publication_status</c>.</summary>
    public required string Name { get; init; }

    /// <summary>What the vocabulary classifies.</summary>
    public required string Description { get; init; }

    /// <summary>The allowed values, in the schema's order.</summary>
    public required IReadOnlyList<VocabularyValue> Values { get; init; }
}
