using System.Text.Json.Nodes;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Converts filters between the contract's <c>FilterNode</c> and the engine's <c>filterNode</c>, which the schema stores in
/// <c>default_filter_json</c> and <c>source_filter_json</c> (OI-54). They differ only in a dimension operand's grouping, which names
/// its level as <c>levelKey</c> (engine <c>level</c>) and its ancestor as <c>memberKey</c> (engine <c>ancestor</c>). Also rewrites a
/// stored filter when a calculation dimension it compares is renamed or deleted.
/// </summary>
public static class ReportFilters
{
    /// <summary>The contract's grouping property names and the engine's.</summary>
    private static readonly (string Contract, string Engine)[] GroupingNames = [("levelKey", "level"), ("memberKey", "ancestor")];

    /// <summary>The stored text of a contract filter.</summary>
    /// <param name="filter">A filter in the contract's shape, or null.</param>
    /// <returns>The filter in the engine's shape, or null.</returns>
    public static string? ToEngine(FilterNode? filter)
    {
        if (filter is null) return null;
        var copy = filter.Json.DeepClone();
        RenameGroupings(copy, toEngine: true);
        return copy.ToJsonString();
    }

    /// <summary>The contract filter of stored text.</summary>
    /// <param name="json">A stored filter, or null.</param>
    /// <returns>The filter in the contract's shape, or null.</returns>
    public static FilterNode? FromEngine(string? json)
    {
        if (json is null) return null;
        var filter = JsonNode.Parse(json)!.AsObject();
        RenameGroupings(filter, toEngine: false);
        return new FilterNode(filter);
    }

    /// <summary>A stored filter with a calculation dimension's operands renamed.</summary>
    /// <param name="json">A stored filter, or null.</param>
    /// <param name="from">The dimension's old key.</param>
    /// <param name="to">The dimension's new key.</param>
    /// <returns>The rewritten filter, or null.</returns>
    public static string? RenameDimension(string? json, string from, string to)
    {
        if (json is null) return null;
        var filter = JsonNode.Parse(json)!;
        foreach (var operand in Objects(filter).Where(node => Names(node, from))) operand["id"] = to;
        return filter.ToJsonString();
    }

    /// <summary>
    /// A stored filter without the predicates that compare a calculation dimension; a logical node keeps its other operands, and a
    /// node left without any goes too.
    /// </summary>
    /// <param name="json">A stored filter, or null.</param>
    /// <param name="key">The deleted dimension's key.</param>
    /// <returns>The rewritten filter, or null when nothing is left.</returns>
    public static string? WithoutDimension(string? json, string key)
    {
        if (json is null) return null;
        return Without(JsonNode.Parse(json)!.AsObject(), key)?.ToJsonString();
    }

    /// <summary>A filter node without the predicates that compare a dimension; null when nothing is left.</summary>
    private static JsonObject? Without(JsonObject node, string key)
    {
        switch ((string)node["operator"]!)
        {
            case "and" or "or":
                JsonNode[] operands = [.. node["operands"]!.AsArray().Select(operand => Without(operand!.AsObject(), key)).OfType<JsonObject>().Select(operand => operand.DeepClone())];
                if (operands.Length == 0) return null;
                node["operands"] = new JsonArray(operands);
                return node;
            case "not":
                var negated = Without(node["operand"]!.AsObject(), key);
                if (negated is null) return null;
                node["operand"] = negated.DeepClone();
                return node;
            default:
                return Objects(node).Any(operand => Names(operand, key)) ? null : node;
        }
    }

    /// <summary>Renames the grouping properties of every dimension operand, towards the engine's names or back.</summary>
    private static void RenameGroupings(JsonNode filter, bool toEngine)
    {
        foreach (var grouping in Objects(filter).Where(node => (string?)node["type"] == "dimension").Select(node => node["grouping"]).OfType<JsonObject>())
        {
            foreach (var (contract, engine) in GroupingNames)
            {
                var (from, to) = toEngine ? (contract, engine) : (engine, contract);
                if (!grouping.Remove(from, out var value)) continue;
                grouping[to] = value;
            }
        }
    }

    /// <summary>Whether a node is an operand comparing a dimension.</summary>
    private static bool Names(JsonObject node, string key)
    {
        return (string?)node["type"] == "dimension" && (string?)node["id"] == key;
    }

    /// <summary>Every object in a JSON tree, the root first.</summary>
    private static IEnumerable<JsonObject> Objects(JsonNode node)
    {
        switch (node)
        {
            case JsonObject value:
                yield return value;
                foreach (var child in value.Select(property => property.Value).OfType<JsonNode>().ToList())
                    foreach (var nested in Objects(child)) yield return nested;
                break;
            case JsonArray items:
                foreach (var item in items.OfType<JsonNode>().ToList())
                    foreach (var nested in Objects(item)) yield return nested;
                break;
        }
    }
}
