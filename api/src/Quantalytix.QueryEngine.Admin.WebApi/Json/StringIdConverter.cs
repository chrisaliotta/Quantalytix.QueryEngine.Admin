using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.WebApi.Json;

/// <summary>Reads and writes a surrogate id as a JSON string of its decimal digits; a JSON number is refused.</summary>
public sealed class StringIdConverter : JsonConverter<long>
{
    /// <summary>The shared instance; the converter holds no state.</summary>
    public static readonly StringIdConverter Instance = new();

    /// <summary>Reads an id from a JSON string.</summary>
    /// <param name="reader">The reader, positioned on the value.</param>
    /// <param name="typeToConvert">Always <see cref="long"/>.</param>
    /// <param name="options">Unused.</param>
    /// <returns>The id.</returns>
    /// <exception cref="JsonException">The value is not a string of decimal digits.</exception>
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("An id must be a JSON string.");
        if (!long.TryParse(reader.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            throw new JsonException("An id must be a string of decimal digits.");
        return id;
    }

    /// <summary>Writes an id as a JSON string.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The id.</param>
    /// <param name="options">Unused.</param>
    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }
}
