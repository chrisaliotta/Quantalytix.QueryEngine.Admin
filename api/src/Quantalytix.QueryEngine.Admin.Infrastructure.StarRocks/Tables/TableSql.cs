using System.Reflection;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Tables;

/// <summary>
/// The SQL of one record's table, built from the record's properties and its catalog entry. Statements without a filter
/// are built once per record type. Parameters are named after record properties, so Dapper binds records and filter
/// objects directly; JSON columns are written through <c>PARSE_JSON</c>.
/// </summary>
/// <typeparam name="TRecord">A record from the Metadata feature.</typeparam>
internal static class TableSql<TRecord> where TRecord : class
{
    /// <summary>The record's table.</summary>
    public static readonly TableDefinition Table = TableCatalog.For<TRecord>();

    /// <summary>The record's properties, in the table's column order.</summary>
    private static readonly IReadOnlyList<PropertyInfo> Properties = DeclaredProperties.Of(typeof(TRecord));

    /// <summary>Every column, in the table's order.</summary>
    private static readonly string Columns = string.Join(", ", Properties.Select(property => SnakeCase.From(property.Name)));

    /// <summary>Selects every column of every row.</summary>
    private static readonly string SelectAll = $"SELECT {Columns} FROM {Table.Name}";

    /// <summary>Sets every non-key column of the row whose key columns equal the record's key properties.</summary>
    public static readonly string Update = BuildUpdate();

    /// <summary>Selects every column of the rows a filter matches.</summary>
    /// <param name="filter">Null for every row; otherwise an anonymous filter object.</param>
    /// <returns>The SELECT statement.</returns>
    public static string Select(object? filter)
    {
        if (filter is null) return SelectAll;
        return $"{SelectAll} WHERE {Where(filter)}";
    }

    /// <summary>Deletes the rows a filter matches.</summary>
    /// <param name="filter">An anonymous filter object.</param>
    /// <returns>The DELETE statement.</returns>
    public static string Delete(object filter)
    {
        return $"DELETE FROM {Table.Name} WHERE {Where(filter)}";
    }

    /// <summary>Inserts several rows with one statement whose parameters are named <c>p{row}_{Property}</c>.</summary>
    /// <param name="rowCount">The number of rows, at least one.</param>
    /// <returns>The multi-row INSERT statement.</returns>
    public static string Insert(int rowCount)
    {
        var rows = Enumerable.Range(0, rowCount)
            .Select(row => $"({string.Join(", ", Properties.Select(property => Value(ParameterName(row, property), property)))})");
        return $"INSERT INTO {Table.Name} ({Columns}) VALUES {string.Join(", ", rows)}";
    }

    /// <summary>The parameter values of <see cref="Insert"/> for the given rows.</summary>
    /// <param name="rows">The rows to insert.</param>
    /// <returns>Every row's property values, keyed by <c>p{row}_{Property}</c>.</returns>
    public static Dictionary<string, object?> InsertParameters(IReadOnlyList<TRecord> rows)
    {
        var parameters = new Dictionary<string, object?>(rows.Count * Properties.Count);
        for (var row = 0; row < rows.Count; row++)
        {
            foreach (var property in Properties) parameters[ParameterName(row, property)] = property.GetValue(rows[row]);
        }

        return parameters;
    }

    /// <summary>Builds <see cref="Update"/>: non-key columns in SET, key columns only in WHERE.</summary>
    private static string BuildUpdate()
    {
        var assignments = Properties
            .Where(property => !Table.KeyProperties.Contains(property.Name))
            .Select(property => $"{SnakeCase.From(property.Name)} = {Value(property.Name, property)}");
        var keys = Table.KeyProperties.Select(key => $"{SnakeCase.From(key)} = @{key}");
        return $"UPDATE {Table.Name} SET {string.Join(", ", assignments)} WHERE {string.Join(" AND ", keys)}";
    }

    /// <summary>The conditions of a filter object: equality, or IN for collections, which Dapper expands.</summary>
    private static string Where(object filter)
    {
        return string.Join(" AND ", TableFilter.PropertiesOf(filter).Select(Condition));
    }

    /// <summary>One filter condition.</summary>
    private static string Condition(PropertyInfo filterProperty)
    {
        var column = SnakeCase.From(filterProperty.Name);
        if (TableFilter.IsCollection(filterProperty)) return $"{column} IN @{filterProperty.Name}";
        return $"{column} = @{filterProperty.Name}";
    }

    /// <summary>A parameter reference, wrapped in <c>PARSE_JSON</c> when the property is a JSON column.</summary>
    private static string Value(string parameterName, PropertyInfo property)
    {
        if (Table.JsonProperties.Contains(property.Name)) return $"PARSE_JSON(@{parameterName})";
        return $"@{parameterName}";
    }

    /// <summary>The name of a row's parameter in a multi-row INSERT.</summary>
    private static string ParameterName(int row, PropertyInfo property)
    {
        return $"p{row}_{property.Name}";
    }
}
