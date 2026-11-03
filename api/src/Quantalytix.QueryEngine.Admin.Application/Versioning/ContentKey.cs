using System.Text.Json;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// Builds the canonical text of a version's content from each table's rows, projected by the copier so that no surrogate id a copy
/// renews remains: rows are serialized and sorted within their table, so neither row order nor ids change the key.
/// </summary>
public static class ContentKey
{
    /// <summary>The canonical text of some tables of rows.</summary>
    /// <param name="tables">Each table's projected rows, in a fixed order per kind.</param>
    /// <returns>Equal for two versions exactly when every table holds the same projected rows.</returns>
    public static string Of(params IEnumerable<object>[] tables)
    {
        return JsonSerializer.Serialize(tables.Select(rows => rows.Select(row => JsonSerializer.Serialize(row, row.GetType())).Order(StringComparer.Ordinal)));
    }

    /// <summary>The key an optional reference to a row of the version names, in place of the id a copy renews.</summary>
    /// <param name="keys">The keys of the referenced table's rows, by id.</param>
    /// <param name="id">The reference, or null.</param>
    /// <returns>The referenced row's key, or null when there is no reference.</returns>
    public static string? KeyOf(IReadOnlyDictionary<long, string> keys, long? id)
    {
        return id is { } value ? keys[value] : null;
    }
}
