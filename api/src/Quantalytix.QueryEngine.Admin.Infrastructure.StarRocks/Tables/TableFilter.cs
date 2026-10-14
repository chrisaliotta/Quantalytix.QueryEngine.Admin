using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Tables;

/// <summary>
/// Interprets the anonymous filter objects of <c>ITableGateway</c>: each property names a record property to match, by
/// equality, or with IN when it holds a collection other than a string.
/// </summary>
internal static class TableFilter
{
    /// <summary>The properties of each filter type seen so far.</summary>
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>> FilterProperties = new();

    /// <summary>Returns a filter's properties, each named after the record property it matches.</summary>
    /// <param name="filter">An anonymous filter object.</param>
    /// <returns>The filter's properties, in declaration order.</returns>
    public static IReadOnlyList<PropertyInfo> PropertiesOf(object filter)
    {
        return FilterProperties.GetOrAdd(filter.GetType(), DeclaredProperties.Of);
    }

    /// <summary>Whether a filter property matches with IN rather than equality.</summary>
    /// <param name="property">A filter property.</param>
    /// <returns>True when the property holds a collection other than a string.</returns>
    public static bool IsCollection(PropertyInfo property)
    {
        return property.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(property.PropertyType);
    }

    /// <summary>Whether a filter cannot match any row because one of its collections is empty.</summary>
    /// <param name="filter">An anonymous filter object.</param>
    /// <returns>True when a collection property holds no values.</returns>
    public static bool MatchesNothing(object filter)
    {
        return PropertiesOf(filter).Any(property => IsCollection(property) && !((IEnumerable)property.GetValue(filter)!).Cast<object>().Any());
    }
}
