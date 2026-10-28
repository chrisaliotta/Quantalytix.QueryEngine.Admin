using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The DDL a dataset change set's commit runs on its managed table before any row is written (OI-33): the rename of
/// <c>renameSourceTable</c>, then the new columns of <c>saveField</c> with <c>createColumn</c>. DDL cannot be rolled back, so a
/// commit that fails afterwards leaves a renamed table or unread columns; a replay of the same operations finds them done.
/// </summary>
public sealed class DatasetSchemaChanges(
    ITableGateway<DatasetVersionRecord> versions,
    ISourceObjectCatalog catalog,
    IManagedTables managedTables,
    DatasetMaterializations materializations)
{
    /// <summary>Runs the DDL of operations a preview has accepted.</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="operations">The change set's operations.</param>
    /// <param name="table">The source object the preview leaves the version reading.</param>
    /// <param name="cancellationToken">Cancels the statements.</param>
    /// <returns>A task that completes when the table is renamed and has every new column.</returns>
    public async Task ApplyAsync(long versionId, IReadOnlyList<DatasetOperation> operations, string table, CancellationToken cancellationToken)
    {
        var stored = (await versions.GetAsync(new { DatasetVersionId = versionId }, cancellationToken)).SourceObject;
        if (operations.OfType<DatasetRenameSourceTableOperation>().Any() && stored != table && (await catalog.ListColumnsAsync(table, cancellationToken)).Count == 0)
            await managedTables.RenameAsync(stored, table, cancellationToken);

        var existing = (await catalog.ListColumnsAsync(table, cancellationToken)).Select(column => column.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        DatasetNewColumnInput[] added =
        [
            .. operations
                .OfType<DatasetSaveFieldOperation>()
                .Select(operation => operation.Input)
                .Where(input => input.CreateColumn == true && existing.Add(input.SourceColumn!))
                .Select(input => new DatasetNewColumnInput
                {
                    Name = input.SourceColumn!,
                    DataType = input.DataType,
                    NumericPrecision = input.NumericPrecision,
                    NumericScale = input.NumericScale,
                    IsNullable = input.IsNullable
                })
        ];
        if (added.Length == 0) return;
        await managedTables.AddColumnsAsync(table, added, cancellationToken);
        await materializations.DefineAsync(table, cancellationToken);
    }
}
