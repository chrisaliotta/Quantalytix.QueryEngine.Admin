namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>
/// A relationship between vocabularies that <c>GET /api/vocabularies</c> serves beside them, such as
/// <c>componentRolesByKind</c>. A module implements it in its own folder with a parameterless constructor;
/// <see cref="VocabularyCatalog"/> finds it.
/// </summary>
public interface IVocabularyRelation
{
    /// <summary>The relationship's key in <c>GET /api/vocabularies</c>.</summary>
    string Key { get; }

    /// <summary>The relationship, serialized as it stands.</summary>
    object Value { get; }
}
