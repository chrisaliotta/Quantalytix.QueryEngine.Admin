using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Quantalytix.QueryEngine.Admin.WebApi.Json;

/// <summary>
/// The API's id convention (blueprint 01, OI-04): every surrogate id is a JSON string. An id is a <see cref="long"/> property
/// whose name is <c>Id</c> or ends with <c>Id</c>, or a list of them whose name ends with <c>Ids</c>, which the naming rule
/// (OI-10) makes true of every id and of nothing else. The modifier gives those properties a string converter, so ids read and
/// write as strings everywhere the API's JSON options apply, including the OpenAPI schema generator.
/// </summary>
public static class StringIds
{
    /// <summary>Gives every id property of a type its string converter; a <see cref="DefaultJsonTypeInfoResolver"/> modifier.</summary>
    /// <param name="typeInfo">The contract of a serialized type.</param>
    public static void Modify(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;
        foreach (var property in typeInfo.Properties)
        {
            var converter = ConverterFor(property.PropertyType, NameOf(property));
            if (converter is not null) property.CustomConverter = converter;
        }
    }

    /// <summary>Whether a property, parameter or route value of a type and name carries ids.</summary>
    /// <param name="type">The CLR type.</param>
    /// <param name="name">The member name, in any case.</param>
    /// <returns>True for a <see cref="long"/> or nullable one named like an id, or a list of longs named like ids.</returns>
    public static bool IsId(Type type, string name)
    {
        return ConverterFor(type, name) is not null;
    }

    /// <summary>The string converter of an id member, or null when the member is not an id.</summary>
    private static JsonConverter? ConverterFor(Type type, string name)
    {
        if (type == typeof(long) && IsIdName(name)) return StringIdConverter.Instance;
        if (type == typeof(long?) && IsIdName(name)) return NullableStringIdConverter.Instance;
        if (type == typeof(IReadOnlyList<long>) && name.EndsWith("Ids", StringComparison.Ordinal)) return StringIdListConverter.Instance;
        return null;
    }

    /// <summary>Whether a member name names one id: <c>id</c>, <c>Id</c> or a name ending with <c>Id</c>.</summary>
    private static bool IsIdName(string name)
    {
        return name.Equals("id", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Id", StringComparison.Ordinal);
    }

    /// <summary>The CLR member name of a property, which keeps its PascalCase whatever the JSON naming policy does.</summary>
    private static string NameOf(JsonPropertyInfo property)
    {
        return property.AttributeProvider is System.Reflection.MemberInfo member ? member.Name : property.Name;
    }
}
