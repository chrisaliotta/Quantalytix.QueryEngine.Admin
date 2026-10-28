using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The contract's <c>FilterNode</c>: the query engine's structured filter in the contract's shape (OI-54), carried as its JSON. The
/// shape is checked by the operation validators and converted for storage by <see cref="ReportFilters"/>.
/// </summary>
/// <param name="Json">The filter's root node.</param>
[JsonConverter(typeof(FilterNodeJsonConverter))]
public sealed record FilterNode(JsonObject Json);

/// <summary>Reads and writes a <see cref="FilterNode"/> as its JSON object.</summary>
public sealed class FilterNodeJsonConverter : JsonConverter<FilterNode>
{
    /// <summary>Reads a filter object.</summary>
    /// <param name="reader">The reader, at the object.</param>
    /// <param name="typeToConvert">Unused.</param>
    /// <param name="options">The serializer options.</param>
    /// <returns>The filter.</returns>
    public override FilterNode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new FilterNode(JsonSerializer.Deserialize<JsonObject>(ref reader, options)!);
    }

    /// <summary>Writes a filter object.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The filter.</param>
    /// <param name="options">The serializer options.</param>
    public override void Write(Utf8JsonWriter writer, FilterNode value, JsonSerializerOptions options)
    {
        value.Json.WriteTo(writer, options);
    }
}
