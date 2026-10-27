using System.Collections.Frozen;
using System.Reflection;

namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>
/// Every controlled vocabulary and vocabulary relationship, found by discovery in the Application assembly: a module adds a
/// vocabulary by declaring a public static <see cref="Vocabulary"/> field, and a relationship by implementing
/// <see cref="IVocabularyRelation"/>, in its own folder. Nothing is listed here (the shared hotspot rule).
/// </summary>
public static class VocabularyCatalog
{
    /// <summary>Every vocabulary definition, by name.</summary>
    public static readonly IReadOnlyList<Vocabulary> All = DiscoverVocabularies();

    /// <summary>Every vocabulary relationship, by key.</summary>
    public static readonly IReadOnlyList<IVocabularyRelation> Relations = DiscoverRelations();

    /// <summary>The vocabularies by name.</summary>
    private static readonly FrozenDictionary<string, Vocabulary> ByName = All.ToFrozenDictionary(vocabulary => vocabulary.Name, StringComparer.Ordinal);

    /// <summary>Finds a vocabulary by its name.</summary>
    /// <param name="name">A vocabulary name, such as <c>publication_status</c>.</param>
    /// <returns>The vocabulary, or null when no vocabulary has the name.</returns>
    public static Vocabulary? Find(string name)
    {
        return ByName.GetValueOrDefault(name);
    }

    /// <summary>The values of every public static <see cref="Vocabulary"/> field; a vocabulary derived from another counts once.</summary>
    private static IReadOnlyList<Vocabulary> DiscoverVocabularies()
    {
        return
        [
            .. typeof(Vocabulary).Assembly.GetTypes()
                .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
                .Where(field => field.FieldType == typeof(Vocabulary))
                .Select(field => (Vocabulary)field.GetValue(null)!)
                .Distinct()
                .OrderBy(vocabulary => vocabulary.Name, StringComparer.Ordinal)
        ];
    }

    /// <summary>One instance of every concrete <see cref="IVocabularyRelation"/>.</summary>
    private static IReadOnlyList<IVocabularyRelation> DiscoverRelations()
    {
        return
        [
            .. typeof(IVocabularyRelation).Assembly.GetTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false } && type.IsAssignableTo(typeof(IVocabularyRelation)))
                .Select(type => (IVocabularyRelation)Activator.CreateInstance(type)!)
                .OrderBy(relation => relation.Key, StringComparer.Ordinal)
        ];
    }
}
