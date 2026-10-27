using System.Text.Json;
using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.WebApi.Json;

/// <summary>Reads and writes a list of surrogate ids as a JSON array of strings.</summary>
public sealed class StringIdListConverter : JsonConverter<IReadOnlyList<long>>
{
    /// <summary>The shared instance; the converter holds no state.</summary>
    public static readonly StringIdListConverter Instance = new();

    /// <summary>Reads a JSON array of id strings.</summary>
    /// <param name="reader">The reader, positioned on the array.</param>
    /// <param name="typeToConvert">Always a list of <see cref="long"/>.</param>
    /// <param name="options">Passed to the id converter.</param>
    /// <returns>The ids, in array order.</returns>
    /// <exception cref="JsonException">The value is not an array of id strings.</exception>
    public override IReadOnlyList<long> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("A list of ids must be a JSON array.");
        var ids = new List<long>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) ids.Add(StringIdConverter.Instance.Read(ref reader, typeof(long), options));
        return ids;
    }

    /// <summary>Writes the ids as a JSON array of strings.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The ids.</param>
    /// <param name="options">Passed to the id converter.</param>
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<long> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var id in value) StringIdConverter.Instance.Write(writer, id, options);
        writer.WriteEndArray();
    }
}
