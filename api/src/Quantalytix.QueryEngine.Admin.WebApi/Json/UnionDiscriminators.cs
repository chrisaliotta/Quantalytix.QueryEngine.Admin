using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Quantalytix.QueryEngine.Admin.WebApi.Json;

/// <summary>
/// Makes a union whose JSON lacks its discriminator a binding error (400), as an unknown discriminator value already is. Without
/// it the serializer tries to create the abstract union type and throws <see cref="NotSupportedException"/>, a 500.
/// </summary>
public static class UnionDiscriminators
{
    /// <summary>Gives an abstract polymorphic type a factory that refuses the missing discriminator; a resolver modifier.</summary>
    /// <param name="typeInfo">The contract of a serialized type.</param>
    public static void Modify(JsonTypeInfo typeInfo)
    {
        if (typeInfo.PolymorphismOptions is not { } union || !typeInfo.Type.IsAbstract) return;
        var values = string.Join(", ", union.DerivedTypes.Select(derived => derived.TypeDiscriminator));
        typeInfo.CreateObject = () => throw new JsonException($"'{union.TypeDiscriminatorPropertyName}' is required and must be one of: {values}.");
    }
}
