using System.Text.Json;

namespace Quantalytix.QueryEngine.Admin.Application.Expressions;

/// <summary>
/// Reads filters in the query contract's structured <c>filterNode</c> form, as report sources' variables and report defaults
/// store them: checks each node's shape the way the contract's schema defines it, and lists the operands the filter compares. The
/// engine trusts published filters, so a filter that passes here is one the engine parses.
/// </summary>
public static class FilterNodes
{
    /// <summary>Checks a filter's shape and lists its operands.</summary>
    /// <param name="json">The filter's JSON text.</param>
    /// <returns>The operands when the filter is well formed; otherwise the first shape error.</returns>
    public static FilterInspection Inspect(string json)
    {
        JsonElement root;
        try
        {
            root = JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (JsonException exception)
        {
            return new FilterInspection { IsValid = false, Error = exception.Message, Operands = [] };
        }

        var operands = new List<FilterOperand>();
        var error = Node(root, "filter", operands);
        return error is null
            ? new FilterInspection { IsValid = true, Operands = operands }
            : new FilterInspection { IsValid = false, Error = error, Operands = [] };
    }

    /// <summary>Checks one filter node; returns its first error, or null.</summary>
    private static string? Node(JsonElement node, string path, List<FilterOperand> operands)
    {
        if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("operator", out var name) || name.ValueKind != JsonValueKind.String)
            return $"{path} must be a filter object with an operator.";
        return name.GetString() switch
        {
            "and" or "or" => List(node, path, "operands", operands, Node),
            "not" => Child(node, path, "operand", out var operand) ?? Node(operand, $"{path}.operand", operands),
            "eq" or "ne" or "lt" or "le" or "gt" or "ge" => Operand(node, path, "left", operands) ?? Operand(node, path, "right", operands),
            "in" => Operand(node, path, "left", operands) ?? List(node, path, "values", operands, OperandAt),
            "isNull" or "isNotNull" => Operand(node, path, "operand", operands),
            var other => $"{path}.operator '{other}' is not a filter operator."
        };
    }

    /// <summary>Checks a non-empty array property, each item with the given check.</summary>
    private static string? List(
        JsonElement node, string path, string property, List<FilterOperand> operands, Func<JsonElement, string, List<FilterOperand>, string?> item)
    {
        if (!node.TryGetProperty(property, out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0)
            return $"{path}.{property} must be a non-empty array.";
        var index = 0;
        foreach (var element in items.EnumerateArray())
        {
            if (item(element, $"{path}.{property}[{index++}]", operands) is { } error) return error;
        }
        return null;
    }

    /// <summary>Checks the operand a node's property holds.</summary>
    private static string? Operand(JsonElement node, string path, string property, List<FilterOperand> operands)
    {
        return Child(node, path, property, out var operand) ?? OperandAt(operand, $"{path}.{property}", operands);
    }

    /// <summary>Checks one operand: a typed reference with an id, or a literal with a value.</summary>
    private static string? OperandAt(JsonElement operand, string path, List<FilterOperand> operands)
    {
        if (operand.ValueKind != JsonValueKind.Object || !operand.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
            return $"{path} must be an operand object with a type.";
        var kind = type.GetString();
        if (kind == "literal") return operand.TryGetProperty("value", out _) ? null : $"{path} is a literal without a value.";
        if (kind is not ("field" or "measure" or "dimension" or "parameter")) return $"{path}.type '{kind}' is not an operand type.";
        if (!operand.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || id.GetString()!.Length == 0)
            return $"{path} must name its {kind} with a non-empty id.";
        operands.Add(new FilterOperand { Type = kind, Id = id.GetString()! });
        return null;
    }

    /// <summary>Reads a required property; returns an error when it is missing.</summary>
    private static string? Child(JsonElement node, string path, string property, out JsonElement child)
    {
        return node.TryGetProperty(property, out child) ? null : $"{path} needs '{property}'.";
    }
}
