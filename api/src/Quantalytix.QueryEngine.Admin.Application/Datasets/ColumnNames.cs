namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>Names the fields created from snake_case physical columns.</summary>
internal static class ColumnNames
{
    /// <summary>The camelCase field key of a column: <c>branch_code</c> becomes <c>branchCode</c>.</summary>
    /// <param name="columnName">The physical column name.</param>
    /// <returns>The field key.</returns>
    public static string ToFieldKey(string columnName)
    {
        var words = Words(columnName);
        return string.Concat(words[0], string.Concat(words.Skip(1).Select(Capitalize)));
    }

    /// <summary>The Title Case display name of a column: <c>branch_code</c> becomes <c>Branch Code</c>.</summary>
    /// <param name="columnName">The physical column name.</param>
    /// <returns>The display name.</returns>
    public static string ToDisplayName(string columnName)
    {
        return string.Join(' ', Words(columnName).Select(Capitalize));
    }

    /// <summary>The lowercase words between the underscores of a column name.</summary>
    private static string[] Words(string columnName)
    {
        return [.. columnName.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(word => word.ToLowerInvariant())];
    }

    /// <summary>A word with its first letter in upper case.</summary>
    private static string Capitalize(string word)
    {
        return $"{char.ToUpperInvariant(word[0])}{word[1..]}";
    }
}
