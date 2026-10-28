using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.WebApi.Reports;

/// <summary>
/// Describes <see cref="FilterNode"/>, which the API carries as its JSON object, as the contract's <c>FilterNode</c>: a union of
/// logical, negation, comparison, membership and null-check nodes over the <c>FilterOperand</c> union (field, measure, parameter,
/// dimension with a <see cref="Grouping"/>, literal). Both unions have discriminators with several values per member, which the
/// schema generator cannot derive from types, so once every component exists this writes the two components by hand.
/// </summary>
internal sealed class FilterNodeSchemaTransformer : IOpenApiDocumentTransformer
{
    /// <summary>The component id of the operand union.</summary>
    private const string OperandId = "FilterOperand";

    /// <summary>Writes the node and operand unions.</summary>
    /// <param name="document">The generated document, with a <see cref="FilterNode"/> component.</param>
    /// <param name="context">Unused.</param>
    /// <param name="cancellationToken">Unused; the transform is synchronous.</param>
    /// <returns>A completed task.</returns>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var components = document.Components!.Schemas!;
        var node = new OpenApiSchemaReference(nameof(FilterNode), document);
        var operand = new OpenApiSchemaReference(OperandId, document);
        components[OperandId] = Union(
            Object(("type", Text()), ("id", Text())),
            Object(("type", Text()), ("id", Text()), ("grouping", new OpenApiSchemaReference(nameof(Grouping), document))),
            Object(("type", Text()), ("value", new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Number | JsonSchemaType.Boolean | JsonSchemaType.Null })));
        components[nameof(FilterNode)] = Union(
            Object(("operator", Text()), ("operands", new OpenApiSchema { Type = JsonSchemaType.Array, Items = node })),
            Object(("operator", Text()), ("operand", node)),
            Object(("operator", Text()), ("left", operand), ("right", operand)),
            Object(("operator", Text()), ("left", operand), ("values", new OpenApiSchema { Type = JsonSchemaType.Array, Items = operand })),
            Object(("operator", Text()), ("operand", operand)));
        return Task.CompletedTask;
    }

    /// <summary>A union of object schemas.</summary>
    private static OpenApiSchema Union(params IOpenApiSchema[] alternatives)
    {
        return new OpenApiSchema { OneOf = [.. alternatives] };
    }

    /// <summary>An object schema that requires the properties it names, except a dimension operand's grouping.</summary>
    private static OpenApiSchema Object(params (string Name, IOpenApiSchema Schema)[] properties)
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = properties.ToDictionary(property => property.Name, property => property.Schema),
            Required = properties.Where(property => property.Name != "grouping").Select(property => property.Name).ToHashSet()
        };
    }

    /// <summary>A text schema.</summary>
    private static OpenApiSchema Text()
    {
        return new OpenApiSchema { Type = JsonSchemaType.String };
    }
}
