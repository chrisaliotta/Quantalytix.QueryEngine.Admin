using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.WebApi.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>
/// Describes <see cref="Vocabularies"/> by the members it writes: an array of terms under each served vocabulary's key and
/// the relationship's schema under each relationship's key, all required. The generator would otherwise describe its
/// extension data as an open object.
/// </summary>
internal sealed class VocabulariesSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>Lists the members of the vocabularies answer.</summary>
    /// <param name="schema">The OpenAPI schema of a type or property.</param>
    /// <param name="context">The transformer context, naming the schema's type.</param>
    /// <param name="cancellationToken">Cancels the schema lookups.</param>
    /// <returns>A task that completes when the members are listed.</returns>
    public async Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type != typeof(Vocabularies)) return;
        var terms = await context.GetOrCreateSchemaAsync(typeof(IReadOnlyList<VocabularyValue>), null, cancellationToken);
        var properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        foreach (var vocabulary in VocabularyCatalog.All.Where(vocabulary => vocabulary.Key is not null)) properties[vocabulary.Key!] = terms;
        foreach (var relation in VocabularyCatalog.Relations)
            properties[relation.Key] = await context.GetOrCreateSchemaAsync(relation.Value.GetType(), null, cancellationToken);
        schema.Properties = properties;
        schema.Required = properties.Keys.ToHashSet(StringComparer.Ordinal);
        schema.AdditionalProperties = null;
        schema.AdditionalPropertiesAllowed = true;
    }
}
