using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Quantalytix.QueryEngine.Admin.Tests.Metadata;

/// <summary>
/// Reads table definitions from StarRocks CREATE TABLE statements written one column per line, as the schema's DDL script
/// writes them.
/// </summary>
internal static partial class SchemaDdl
{
    /// <summary>Parses every CREATE TABLE statement of a DDL script.</summary>
    /// <param name="script">The DDL text.</param>
    /// <returns>The tables by name.</returns>
    public static FrozenDictionary<string, DdlTable> Parse(string script)
    {
        return CreateTablePattern().Matches(script).Select(ToTable).ToFrozenDictionary(table => table.Name, StringComparer.Ordinal);
    }

    /// <summary>The table one CREATE TABLE match declares.</summary>
    private static DdlTable ToTable(Match statement)
    {
        var columnLines = statement.Groups["columns"].Value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new DdlTable
        {
            Name = statement.Groups["name"].Value,
            Columns = [.. columnLines.Select(ToColumn)],
            PrimaryKey = [.. statement.Groups["keys"].Value.Split(',', StringSplitOptions.TrimEntries)]
        };
    }

    /// <summary>The column one definition line declares, such as <c>lower_decimal DECIMAL(38, 12) NULL,</c>.</summary>
    private static DdlColumn ToColumn(string definition)
    {
        var words = definition.Split(' ', 3);
        return new DdlColumn
        {
            Name = words[0],
            Type = words[1].Split('(')[0].TrimEnd(','),
            IsNullable = !definition.Contains("NOT NULL", StringComparison.Ordinal)
        };
    }

    /// <summary>A CREATE TABLE statement: its name, its column list and its PRIMARY KEY columns.</summary>
    [GeneratedRegex(@"CREATE TABLE (?<name>\w+)\s*\((?<columns>.*?)\)\s*PRIMARY KEY \((?<keys>[^)]*)\)", RegexOptions.Singleline)]
    private static partial Regex CreateTablePattern();
}
