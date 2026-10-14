namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>One allowed value of a controlled vocabulary.</summary>
public sealed record VocabularyValue
{
    /// <summary>The value exactly as stored in the metadata column.</summary>
    public required string Value { get; init; }

    /// <summary>The schema's definition of the value, condensed to one sentence.</summary>
    public required string Description { get; init; }
}
