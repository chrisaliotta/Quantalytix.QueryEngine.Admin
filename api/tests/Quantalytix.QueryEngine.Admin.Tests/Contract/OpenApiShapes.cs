using Microsoft.OpenApi;

namespace Quantalytix.QueryEngine.Admin.Tests.Contract;

/// <summary>
/// Compares an operation of the contract with the generated operation of the same path and method: the status codes, and the
/// request and response body schemas by property names, types and nullability, recursively. A schema's <c>allOf</c> parts are
/// merged, later parts overriding earlier ones (how the contract specializes generic envelopes), and a <c>oneOf</c> of one schema
/// and <c>null</c> is that schema, nullable. A contract schema that states no type (a generic slot such as <c>state</c>, or a
/// <c>const</c>) constrains nothing. Names, descriptions, enums, formats and <c>required</c> are not compared.
/// </summary>
internal static class OpenApiShapes
{
    /// <summary>The media type of JSON bodies.</summary>
    private const string Json = "application/json";

    /// <summary>The media type of problem bodies.</summary>
    private const string ProblemJson = "application/problem+json";

    /// <summary>Every difference between a contract operation and its generated counterpart.</summary>
    /// <param name="contract">The contract's operation.</param>
    /// <param name="generated">The generated operation.</param>
    /// <returns>One line per difference; empty when they conform.</returns>
    public static IReadOnlyList<string> Differences(OpenApiOperation contract, OpenApiOperation generated)
    {
        var differences = new List<string>();
        var contractStatuses = contract.Responses?.Keys.Order(StringComparer.Ordinal).ToList() ?? [];
        var generatedStatuses = generated.Responses?.Keys.Order(StringComparer.Ordinal).ToList() ?? [];
        if (!contractStatuses.SequenceEqual(generatedStatuses))
            differences.Add($"status codes: contract [{string.Join(", ", contractStatuses)}], generated [{string.Join(", ", generatedStatuses)}]");

        var comparer = new Comparer(differences);
        comparer.Compare(BodySchema(contract.RequestBody?.Content), BodySchema(generated.RequestBody?.Content), "request");
        foreach (var status in contractStatuses.Intersect(generatedStatuses))
            comparer.Compare(BodySchema(contract.Responses![status].Content), BodySchema(generated.Responses![status].Content), status);
        return differences;
    }

    /// <summary>The schema of a JSON or problem body, or null when there is no body.</summary>
    private static IOpenApiSchema? BodySchema(IDictionary<string, OpenApiMediaType>? content)
    {
        if (content is null) return null;
        if (content.TryGetValue(Json, out var json)) return json.Schema;
        return content.TryGetValue(ProblemJson, out var problem) ? problem.Schema : null;
    }

    /// <summary>The comparable shape of a schema, with <c>allOf</c> merged and a nullable <c>oneOf</c> unwrapped.</summary>
    /// <param name="Type">The schema's types without <c>null</c>, or null when it states none.</param>
    /// <param name="Nullable">Whether <c>null</c> is allowed.</param>
    /// <param name="Properties">The object properties, merged across <c>allOf</c>.</param>
    /// <param name="Items">The array items' schema.</param>
    /// <param name="AdditionalProperties">The map values' schema.</param>
    /// <param name="Alternatives">The alternatives of a union other than <c>null</c>.</param>
    private sealed record Shape(
        JsonSchemaType? Type,
        bool Nullable,
        IReadOnlyDictionary<string, IOpenApiSchema> Properties,
        IOpenApiSchema? Items,
        IOpenApiSchema? AdditionalProperties,
        IReadOnlyList<IOpenApiSchema> Alternatives)
    {
        /// <summary>The shape of a schema.</summary>
        public static Shape Of(IOpenApiSchema schema)
        {
            var alternatives = (schema.OneOf ?? []).Concat(schema.AnyOf ?? []).ToList();
            var nonNull = alternatives.Where(alternative => alternative.Type != JsonSchemaType.Null).ToList();
            if (alternatives.Count > 0 && nonNull.Count == 1)
                return Of(nonNull[0]) with { Nullable = true };

            var properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
            var type = schema.Type;
            IOpenApiSchema? items = schema.Items;
            IOpenApiSchema? additional = schema.AdditionalProperties;
            foreach (var part in schema.AllOf ?? [])
            {
                var merged = Of(part);
                foreach (var (name, property) in merged.Properties) properties[name] = property;
                type ??= merged.Type;
                items ??= merged.Items;
                additional ??= merged.AdditionalProperties;
            }

            foreach (var (name, property) in schema.Properties ?? new Dictionary<string, IOpenApiSchema>()) properties[name] = property;
            var nullable = type is { } flags && flags.HasFlag(JsonSchemaType.Null);
            JsonSchemaType? withoutNull = type is { } t && (t & ~JsonSchemaType.Null) != 0 ? t & ~JsonSchemaType.Null : null;
            return new Shape(withoutNull, nullable || nonNull.Count < alternatives.Count, properties, items, additional, nonNull);
        }
    }

    /// <summary>Walks two schemas side by side and records where they differ.</summary>
    /// <param name="differences">Where the differences go.</param>
    private sealed class Comparer(List<string> differences)
    {
        /// <summary>The pairs of referenced schemas already compared, which keeps recursive schemas finite.</summary>
        private readonly HashSet<(string, string)> _visited = [];

        /// <summary>Compares a contract schema with a generated one at a path such as <c>200.items.code</c>.</summary>
        public void Compare(IOpenApiSchema? contract, IOpenApiSchema? generated, string at)
        {
            if (contract is null) return;
            if (generated is null)
            {
                differences.Add($"{at}: the generated document has no body");
                return;
            }

            if (contract is OpenApiSchemaReference { Reference.Id: { } contractId } && generated is OpenApiSchemaReference { Reference.Id: { } generatedId }
                && !_visited.Add((contractId, generatedId)))
                return;

            var expected = Shape.Of(contract);
            var actual = Shape.Of(generated);
            if (expected.Alternatives.Count > 0)
            {
                CompareUnions(expected, actual, at);
                return;
            }

            if (expected.Type is null && expected.Properties.Count == 0) return;
            if (expected.Type is { } type && actual.Type != type) differences.Add($"{at}: type {type}, generated {actual.Type?.ToString() ?? "none"}");
            if (expected.Nullable != actual.Nullable) differences.Add($"{at}: nullable {expected.Nullable}, generated {actual.Nullable}");
            foreach (var name in expected.Properties.Keys.Except(actual.Properties.Keys)) differences.Add($"{at}.{name}: missing from the generated schema");
            foreach (var name in actual.Properties.Keys.Except(expected.Properties.Keys)) differences.Add($"{at}.{name}: not in the contract");
            foreach (var name in expected.Properties.Keys.Intersect(actual.Properties.Keys))
                Compare(expected.Properties[name], actual.Properties[name], $"{at}.{name}");
            Compare(expected.Items, actual.Items, $"{at}[]");
            Compare(expected.AdditionalProperties, actual.AdditionalProperties, $"{at}{{}}");
        }

        /// <summary>Compares two unions, pairing alternatives that have the same property names.</summary>
        private void CompareUnions(Shape expected, Shape actual, string at)
        {
            if (expected.Alternatives.Count != actual.Alternatives.Count)
            {
                differences.Add($"{at}: {expected.Alternatives.Count} alternatives, generated {actual.Alternatives.Count}");
                return;
            }

            var unmatched = actual.Alternatives.ToList();
            foreach (var alternative in expected.Alternatives)
            {
                var names = Shape.Of(alternative).Properties.Keys.Order(StringComparer.Ordinal).ToList();
                var match = unmatched.FirstOrDefault(candidate => Shape.Of(candidate).Properties.Keys.Order(StringComparer.Ordinal).SequenceEqual(names));
                if (match is null)
                {
                    differences.Add($"{at}: no generated alternative with properties [{string.Join(", ", names)}]");
                    continue;
                }

                unmatched.Remove(match);
                Compare(alternative, match, $"{at}|{string.Join(",", names)}");
            }
        }
    }
}
