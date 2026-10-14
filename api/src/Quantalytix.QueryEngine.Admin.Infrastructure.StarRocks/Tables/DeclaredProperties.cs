using System.Reflection;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Tables;

/// <summary>Reads the public properties of records and filter objects in the order they are declared.</summary>
internal static class DeclaredProperties
{
    /// <summary>
    /// Returns a type's public instance properties in declaration order, which for a metadata record is the column order of
    /// its table. Reflection does not promise that order, so it is restored from the metadata tokens.
    /// </summary>
    /// <param name="type">A record or anonymous filter type.</param>
    /// <returns>The properties, in declaration order.</returns>
    public static IReadOnlyList<PropertyInfo> Of(Type type)
    {
        return [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(property => property.MetadataToken)];
    }
}
