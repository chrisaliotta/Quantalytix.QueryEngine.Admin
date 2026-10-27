using System.Text.Json;
using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.WebApi.Json;

/// <summary>Reads and writes a map of surrogate ids by string key as a JSON object of id strings.</summary>
public sealed class StringIdMapConverter : JsonConverter<IReadOnlyDictionary<string, long>>
{
    /// <summary>The shared instance; the converter holds no state.</summary>
    public static readonly StringIdMapConverter Instance = new();

    /// <summary>Reads a JSON object of id strings.</summary>
    /// <param name="reader">The reader, positioned on the object.</param>
    /// <param name="typeToConvert">Always a map of <see cref="long"/> by string.</param>
    /// <param name="options">Passed to the id converter.</param>
    /// <returns>The ids, by key.</returns>
    /// <exception cref="JsonException">The value is not an object of id strings.</exception>
    public override IReadOnlyDictionary<string, long> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("A map of ids must be a JSON object.");
        var ids = new Dictionary<string, long>(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var key = reader.GetString()!;
            reader.Read();
            ids[key] = StringIdConverter.Instance.Read(ref reader, typeof(long), options);
        }

        return ids;
    }

    /// <summary>Writes the ids as a JSON object of strings.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The ids, by key.</param>
    /// <param name="options">Passed to the id converter.</param>
    public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<string, long> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (key, id) in value)
        {
            writer.WritePropertyName(key);
            StringIdConverter.Instance.Write(writer, id, options);
        }

        writer.WriteEndObject();
    }
}
