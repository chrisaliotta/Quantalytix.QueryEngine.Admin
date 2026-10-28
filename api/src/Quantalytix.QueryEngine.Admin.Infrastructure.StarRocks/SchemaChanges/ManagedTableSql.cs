using Quantalytix.QueryEngine.Admin.Application.Datasets;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SchemaChanges;

/// <summary>
/// The DDL of managed tables (OI-33). A managed table is a duplicate-key table distributed at random whose key is a load timestamp
/// the API adds, because StarRocks needs a key column and a new dataset may start with no columns of its own.
/// </summary>
internal static class ManagedTableSql
{
    /// <summary>The load timestamp column every managed table starts with.</summary>
    public const string LoadedAtColumn = "qe_loaded_at";

    /// <summary>Creates a managed table.</summary>
    /// <param name="tableName">The table.</param>
    /// <param name="columns">Its columns after the load timestamp.</param>
    /// <returns>The statement.</returns>
    public static string Create(string tableName, IReadOnlyList<DatasetNewColumnInput> columns)
    {
        IEnumerable<string> definitions = [$"{StarRocksDdl.Quote(LoadedAtColumn)} DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP", .. columns.Select(Column)];
        return $"CREATE TABLE {StarRocksDdl.Quote(tableName)} ({string.Join(", ", definitions)}) DUPLICATE KEY({StarRocksDdl.Quote(LoadedAtColumn)}) DISTRIBUTED BY RANDOM";
    }

    /// <summary>Adds columns to a table in one schema change.</summary>
    /// <param name="tableName">The table.</param>
    /// <param name="columns">The new columns.</param>
    /// <returns>The statement.</returns>
    public static string AddColumns(string tableName, IReadOnlyList<DatasetNewColumnInput> columns)
    {
        return $"ALTER TABLE {StarRocksDdl.Quote(tableName)} ADD COLUMN ({string.Join(", ", columns.Select(Column))})";
    }

    /// <summary>Renames a table.</summary>
    /// <param name="tableName">The table.</param>
    /// <param name="newName">Its new name.</param>
    /// <returns>The statement.</returns>
    public static string Rename(string tableName, string newName)
    {
        return $"ALTER TABLE {StarRocksDdl.Quote(tableName)} RENAME {StarRocksDdl.Quote(newName)}";
    }

    /// <summary>A column definition.</summary>
    private static string Column(DatasetNewColumnInput column)
    {
        var nullability = column.IsNullable ? "NULL" : "NOT NULL";
        return $"{StarRocksDdl.Quote(column.Name)} {StarRocksDdl.ColumnType(column.DataType, column.NumericPrecision, column.NumericScale)} {nullability}";
    }
}
