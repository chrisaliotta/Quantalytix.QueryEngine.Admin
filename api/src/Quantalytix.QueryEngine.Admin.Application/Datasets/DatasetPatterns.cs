using System.Text.RegularExpressions;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The identifier patterns of dataset metadata. Column names and JSON paths are inlined into DDL, which binds no
/// parameters, so their patterns admit no quotes. Each pattern must match the whole text, a trailing newline included.
/// </summary>
internal static partial class DatasetPatterns
{
    /// <summary>The validation message of a value that does not match <see cref="DatasetKey"/>.</summary>
    public const string DatasetKeyMessage = "'{PropertyName}' must start with a letter or digit and contain only letters, digits, '_', '.' and '-'.";

    /// <summary>The validation message of a value that does not match <see cref="Key"/>.</summary>
    public const string KeyMessage = "'{PropertyName}' must start with a letter and contain only letters, digits and '_'.";

    /// <summary>The validation message of a value that does not match <see cref="ColumnName"/>.</summary>
    public const string ColumnNameMessage = "'{PropertyName}' must start with a letter or '_' and contain only letters, digits and '_'.";

    /// <summary>The validation message of a value that does not match <see cref="JsonPath"/>.</summary>
    public const string JsonPathMessage = "'{PropertyName}' must be '$' followed by member names and array indexes, such as $.address.lines[0].";

    /// <summary>A dataset key, such as <c>mortgage.loan_monthly</c>: a letter or digit, then letters, digits, <c>_</c>, <c>.</c> or <c>-</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.-]*\z")]
    public static partial Regex DatasetKey();

    /// <summary>A field or grain key: a letter, then letters, digits or <c>_</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*\z")]
    public static partial Regex Key();

    /// <summary>A physical column name: a letter or <c>_</c>, then letters, digits or <c>_</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*\z")]
    public static partial Regex ColumnName();

    /// <summary>A JSON path of member names and array indexes, such as <c>$.address.lines[0]</c>.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"^\$(\.[A-Za-z_][A-Za-z0-9_]*|\[[0-9]+\])+\z")]
    public static partial Regex JsonPath();
}
