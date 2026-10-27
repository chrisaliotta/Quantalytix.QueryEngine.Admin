using System.Text.Json;
using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.WebApi.Json;

/// <summary>Reads and writes an optional surrogate id as a JSON string or <c>null</c>.</summary>
public sealed class NullableStringIdConverter : JsonConverter<long?>
{
    /// <summary>The shared instance; the converter holds no state.</summary>
    public static readonly NullableStringIdConverter Instance = new();

    /// <summary>Null is handled here, so a JSON <c>null</c> reaches <see cref="Read"/>.</summary>
    public override bool HandleNull => true;

    /// <summary>Reads an id from a JSON string, or null from a JSON <c>null</c>.</summary>
    /// <param name="reader">The reader, positioned on the value.</param>
    /// <param name="typeToConvert">Always <see cref="Nullable{T}"/> of <see cref="long"/>.</param>
    /// <param name="options">Passed to the id converter.</param>
    /// <returns>The id, or null.</returns>
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        return StringIdConverter.Instance.Read(ref reader, typeof(long), options);
    }

    /// <summary>Writes an id as a JSON string, or a JSON <c>null</c>.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The id, or null.</param>
    /// <param name="options">Passed to the id converter.</param>
    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        StringIdConverter.Instance.Write(writer, value.Value, options);
    }
}
