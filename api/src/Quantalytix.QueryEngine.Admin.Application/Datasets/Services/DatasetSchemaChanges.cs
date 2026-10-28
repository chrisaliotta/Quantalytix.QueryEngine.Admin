using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The DDL of a dataset change set's commit on managed tables (OI-33), planned as <see cref="WritePlan"/> schema statements so it
/// runs before any row is written: per managed table, the rename of <c>renameSourceTable</c>, then the columns <c>saveField</c> with
/// <c>createColumn</c> added, then the redefinition of the table's materialized view. The operations leave the physical catalog of
/// the snapshot's surroundings as the commit leaves the database, so the DDL is the difference between the stored and the changed
/// catalog. DDL cannot be rolled back; a replay of the same operations finds it done and plans none.
/// </summary>
/// <param name="managedTables">Runs the managed-table DDL.</param>
/// <param name="materializations">Redefines a table's materialized view over its new columns.</param>
public sealed class DatasetSchemaChanges(IManagedTables managedTables, DatasetMaterializations materializations)
{
    /// <summary>Adds the schema statements that turn the stored managed tables into the changed ones.</summary>
    /// <param name="before">The aggregate as stored.</param>
    /// <param name="after">The aggregate with every operation applied.</param>
    /// <param name="plan">The plan to add the statements to.</param>
    public void Plan(DatasetSnapshot before, DatasetSnapshot after, WritePlan plan)
    {
        var stored = before.ManagedObjects.ToDictionary(row => row.ManagedSourceObjectId);
        foreach (var managed in after.ManagedObjects.Where(row => row.SourceObjectKind == SourceObjectKinds.Table))
        {
            var from = stored[managed.ManagedSourceObjectId].SourceObject;
            var to = managed.SourceObject;
            var renames = from != to && before.Surroundings.SourceObjects.Any(item => item.Name == from);
            if (renames) plan.Schema(cancellationToken => managedTables.RenameAsync(from, to, cancellationToken));

            var existing = (before.Surroundings.Columns.GetValueOrDefault(renames ? from : to) ?? [])
                .Select(column => column.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            DatasetNewColumnInput[] added =
            [
                .. (after.Surroundings.Columns.GetValueOrDefault(to) ?? []).Where(column => !existing.Contains(column.Name)).Select(NewColumn)
            ];
            if (added.Length == 0) continue;
            plan.Schema(cancellationToken => managedTables.AddColumnsAsync(to, added, cancellationToken));
            plan.Schema(cancellationToken => materializations.DefineAsync(to, cancellationToken));
        }
    }

    /// <summary>The DDL input of a column an operation added to the physical catalog, typed by the field that created it.</summary>
    private static DatasetNewColumnInput NewColumn(PhysicalColumn column)
    {
        var type = ColumnTypeMapping.Map(column.ColumnType)!;
        return new DatasetNewColumnInput
        {
            Name = column.Name,
            DataType = type.DataType,
            NumericPrecision = type.NumericPrecision,
            NumericScale = type.NumericScale,
            IsNullable = column.IsNullable
        };
    }
}
