using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Names the materialized view of a source object and the view column of a JSON path field (OI-32). A column's name follows from
/// what it extracts, so fields of different versions that extract the same value share one column.
/// </summary>
public static partial class MaterializedViewNames
{
    /// <summary>The length of <c>qe_field.materialized_column</c>.</summary>
    private const int ColumnLength = 128;

    /// <summary>The prefix of every view; <c>qe_</c> keeps the views out of the source object list.</summary>
    private const string ViewPrefix = "qe_mv_";

    /// <summary>The materialized view of a source object.</summary>
    /// <param name="sourceObject">The table or view.</param>
    /// <returns>The view's name.</returns>
    public static string ViewOf(string sourceObject)
    {
        return ViewPrefix + sourceObject;
    }

    /// <summary>The view column of a JSON path field: its JSON column, path and type, hashed when that is too long.</summary>
    /// <param name="field">A JSON path field.</param>
    /// <returns>The column's name.</returns>
    public static string ColumnOf(FieldRecord field)
    {
        var path = NonWord().Replace(field.JsonPath!.TrimStart('$'), "_").Trim('_');
        var type = field.DataType == Vocabulary.DataTypes.Decimal
            ? string.Create(CultureInfo.InvariantCulture, $"{field.DataType}_{field.NumericPrecision}_{field.NumericScale}")
            : field.DataType;
        var name = $"{field.SourceColumn}_{path}_{type}".ToLowerInvariant();
        if (name.Length <= ColumnLength) return name;
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{field.SourceColumn}|{field.JsonPath}|{type}")))[..8];
        return $"{name[..(ColumnLength - hash.Length - 1)]}_{hash}";
    }

    /// <summary>Runs of characters a column name does not take.</summary>
    [GeneratedRegex("[^A-Za-z0-9]+")]
    private static partial Regex NonWord();
}
