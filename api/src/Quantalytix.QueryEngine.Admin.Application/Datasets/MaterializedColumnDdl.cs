using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Builds the DDL that materializes a <c>json_path</c> field as a generated column. The query engine reads a set
/// materialized column instead of extracting the JSON value (FieldSql in its StarRocks provider), so the column must hold
/// exactly what FieldSql computes: <c>get_json_string</c> of the JSON column at the path, cast to the field's type.
/// </summary>
public static class MaterializedColumnDdl
{
    /// <summary>Builds the <c>ALTER TABLE ... ADD COLUMN ... AS ...</c> statement that materializes a field.</summary>
    /// <param name="tableName">The version's source table.</param>
    /// <param name="columnName">The new column's name, already checked against the column name pattern.</param>
    /// <param name="field">The <c>json_path</c> field, whose JSON path is already checked against its pattern.</param>
    /// <returns>The statement, with identifiers quoted and the JSON path inlined, because DDL binds no parameters.</returns>
    public static string AddColumn(string tableName, string columnName, FieldRecord field)
    {
        var columnType = ColumnType(field);
        var value = $"get_json_string({Quote(field.SourceColumn!)}, '{field.JsonPath}')";
        if (field.DataType != DataTypes.String) value = $"CAST({value} AS {columnType})";
        return $"ALTER TABLE {Quote(tableName)} ADD COLUMN {Quote(columnName)} {columnType} NULL AS {value}";
    }

    /// <summary>The column type of a field's data type. FieldSql casts the extracted text to these same types; strings stay text.</summary>
    private static string ColumnType(FieldRecord field)
    {
        return field.DataType switch
        {
            DataTypes.String => "VARCHAR(65533)",
            DataTypes.Boolean => "BOOLEAN",
            DataTypes.Int32 => "INT",
            DataTypes.Int64 => "BIGINT",
            DataTypes.Decimal => string.Create(CultureInfo.InvariantCulture, $"DECIMAL({field.NumericPrecision}, {field.NumericScale})"),
            DataTypes.Date => "DATE",
            DataTypes.Json => "JSON",
            _ => "DATETIME"
        };
    }

    /// <summary>Quotes an identifier with backticks, doubling any backtick inside it, as the engine's SQL does.</summary>
    private static string Quote(string identifier)
    {
        return $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";
    }
}
