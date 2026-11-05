namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>
/// A controlled vocabulary: the closed set of values a metadata column accepts. Declared as a public static field of type
/// <see cref="Vocabulary"/>, conventionally <c>Definition</c>, anywhere in the Application assembly; <see cref="VocabularyCatalog"/>
/// finds it.
/// </summary>
public sealed record Vocabulary
{
    /// <summary>The schema's vocabulary name, such as <c>binding_kind</c>, which validator messages quote.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The vocabulary's key in <c>GET /api/vocabularies</c>, such as <c>bindingKind</c>; null for a vocabulary only
    /// validators read.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>What the vocabulary classifies.</summary>
    public required string Description { get; init; }

    /// <summary>The allowed values, in the schema's order.</summary>
    public required IReadOnlyList<VocabularyValue> Values { get; init; }

    /// <summary>Whether a value comes no earlier than another in a vocabulary that lists versions oldest first.</summary>
    /// <param name="value">A value of the vocabulary.</param>
    /// <param name="earliest">The value it must reach.</param>
    /// <returns>True when <paramref name="value"/> is <paramref name="earliest"/> or later.</returns>
    public bool IsAtLeast(string value, string earliest)
    {
        var order = Values.Select(item => item.Value).ToList();
        return order.IndexOf(value) >= order.IndexOf(earliest);
    }
}
