using System.Text.Json.Serialization;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.WebApi.Vocabulary;

/// <summary>
/// The answer of <c>GET /api/vocabularies</c> (the contract's <c>Vocabularies</c>, OI-11): every served vocabulary's terms
/// under its key, and every vocabulary relationship under its key, as top-level members. Its OpenAPI schema lists the same
/// members (<c>VocabulariesSchemaTransformer</c>).
/// </summary>
public sealed class Vocabularies
{
    /// <summary>The members, by key: a list of terms for a vocabulary, the relationship's value for a relationship.</summary>
    [JsonExtensionData]
    public Dictionary<string, object> Members { get; } = new(StringComparer.Ordinal);

    /// <summary>The vocabularies and relationships of <see cref="VocabularyCatalog"/> that have a key.</summary>
    /// <returns>The answer.</returns>
    public static Vocabularies FromCatalog()
    {
        var answer = new Vocabularies();
        foreach (var vocabulary in VocabularyCatalog.All.Where(vocabulary => vocabulary.Key is not null)) answer.Members.Add(vocabulary.Key!, vocabulary.Values);
        foreach (var relation in VocabularyCatalog.Relations) answer.Members.Add(relation.Key, relation.Value);
        return answer;
    }
}
