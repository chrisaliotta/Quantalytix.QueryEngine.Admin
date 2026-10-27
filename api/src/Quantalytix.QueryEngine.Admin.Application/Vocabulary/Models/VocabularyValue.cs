namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>One allowed value of a controlled vocabulary: the contract's <c>VocabularyTerm</c>.</summary>
public sealed record VocabularyValue
{
    /// <summary>The value exactly as stored in the metadata column.</summary>
    public required string Value { get; init; }

    /// <summary>The value as a select shows it.</summary>
    public required string Label { get; init; }

    /// <summary>The schema's definition of the value, condensed to one sentence.</summary>
    public required string Description { get; init; }
}
