using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>An <see cref="IManagedTables"/> that records each DDL call and applies it to a fixed catalog.</summary>
/// <param name="catalog">The catalog the DDL changes.</param>
internal sealed class RecordingManagedTables(FixedSourceObjectCatalog catalog) : IManagedTables
{
    /// <summary>The calls, in order, such as <c>add loan_daily risk_band</c>.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>Records the create and adds the table.</summary>
    /// <param name="tableName">The table.</param>
    /// <param name="columns">Its columns.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task CreateAsync(string tableName, IReadOnlyList<DatasetNewColumnInput> columns, CancellationToken cancellationToken)
    {
        Calls.Add($"create {tableName}");
        catalog.With(SourceObjectKinds.Table, tableName, [.. columns.Select(column => (column.Name, ColumnTypeMapping.ColumnTypeOf(column.DataType, column.NumericPrecision, column.NumericScale)))]);
        return Task.CompletedTask;
    }

    /// <summary>Records the columns and adds them.</summary>
    /// <param name="tableName">The table.</param>
    /// <param name="columns">The new columns.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task AddColumnsAsync(string tableName, IReadOnlyList<DatasetNewColumnInput> columns, CancellationToken cancellationToken)
    {
        foreach (var column in columns)
        {
            Calls.Add($"add {tableName} {column.Name}");
            catalog.AddColumn(tableName, new PhysicalColumn
            {
                Name = column.Name,
                ColumnType = ColumnTypeMapping.ColumnTypeOf(column.DataType, column.NumericPrecision, column.NumericScale),
                IsNullable = column.IsNullable,
                IsGenerated = false
            });
        }

        return Task.CompletedTask;
    }

    /// <summary>Records the rename and renames the table.</summary>
    /// <param name="tableName">The table.</param>
    /// <param name="newName">Its new name.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task RenameAsync(string tableName, string newName, CancellationToken cancellationToken)
    {
        Calls.Add($"rename {tableName} {newName}");
        catalog.Rename(tableName, newName);
        return Task.CompletedTask;
    }

    /// <summary>Records the drop and removes the table.</summary>
    /// <param name="tableName">The table.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task DropAsync(string tableName, CancellationToken cancellationToken)
    {
        Calls.Add($"drop {tableName}");
        catalog.Remove(tableName);
        return Task.CompletedTask;
    }
}
