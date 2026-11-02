using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The dataset manager's commands, which run on stored state, never inside a change set: create a dataset with its live version over an
/// existing object or a new managed table (OI-33), and delete it with its live version and the managed tables only it reads. Both
/// change what the engine reads, so both advance the semantic revision.
/// </summary>
public sealed class DatasetCommands(
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> versions,
    ITableGateway<FieldRecord> fields,
    ITableGateway<ManagedSourceObjectRecord> managed,
    DatasetVersionContentStore content,
    LiveSnapshot live,
    SnapshotHolders holders,
    DatasetQueries queries,
    SourceObjectDirectory sourceObjects,
    IManagedTables managedTables,
    DatasetTableDrops tableDrops,
    SemanticRevision semanticRevision,
    IIdGenerator ids,
    TimeProvider time)
{
    /// <summary>The expression grammar version of a new dataset: the one the query engine parses.</summary>
    private const string ExpressionLanguageVersion = "1.0";

    /// <summary>
    /// Creates a dataset with a live version whose fields read every column fields can read. A new managed table is created and
    /// registered first: DDL cannot be rolled back, and a table without a dataset is a residue the registry still names.
    /// </summary>
    /// <param name="input">The new dataset, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads, statements and writes.</param>
    /// <returns>The list item, or a conflict when the key or the table name is taken.</returns>
    public async Task<Outcome<DatasetListItem>> CreateAsync(DatasetCreateInput input, CancellationToken cancellationToken)
    {
        if ((await datasets.ListAsync(null, cancellationToken)).Any(row => string.Equals(row.DatasetKey, input.Key, StringComparison.OrdinalIgnoreCase)))
            return Outcome<DatasetListItem>.Refused(Refusal.Conflict("Key taken", $"Another dataset already uses the key '{input.Key}'."));

        var dataset = new DatasetRecord { DatasetId = ids.NextId(), DatasetKey = input.Key, DisplayName = input.DisplayName.Trim() };
        var (kind, sourceObject) = input switch
        {
            DatasetCreateFromExistingInput existing => (existing.SourceObjectKind, existing.SourceObject),
            DatasetCreateWithNewTableInput created => (SourceObjectKinds.Table, created.TableName),
            _ => throw new ArgumentOutOfRangeException(nameof(input), input, "Not a dataset create input.")
        };
        if (input is DatasetCreateWithNewTableInput table)
        {
            if ((await sourceObjects.ObjectsAsync(cancellationToken)).Any(item => item.Name == table.TableName))
                return Outcome<DatasetListItem>.Refused(Refusal.Conflict("Table name taken", $"The tenant database already has an object named '{table.TableName}'."));
            await managedTables.CreateAsync(table.TableName, table.Columns, cancellationToken);
            await managed.InsertAsync(
                new ManagedSourceObjectRecord
                {
                    ManagedSourceObjectId = ids.NextId(),
                    SourceObject = table.TableName,
                    SourceObjectKind = SourceObjectKinds.Table,
                    CreatedByDatasetId = dataset.DatasetId,
                    CreatedAt = time.GetUtcNow().UtcDateTime
                },
                cancellationToken);
        }

        await datasets.InsertAsync(dataset, cancellationToken);
        var version = await InsertLiveAsync(dataset.DatasetId, kind, sourceObject, cancellationToken);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        await fields.InsertManyAsync(
            [
                .. (await ReadableColumnsAsync(input, cancellationToken))
                    .Select(column => ColumnFields.Of(ids.NextId(), version.DatasetVersionId, ColumnFields.KeyOf(column.Name, keys), column.Name, column.Type, column.IsNullable))
            ],
            cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return Outcome<DatasetListItem>.Success(await queries.GetAsync(dataset.DatasetId, cancellationToken));
    }

    /// <summary>The columns the live version gets a field for: a new table's requested columns, or every readable column of an existing object.</summary>
    private async Task<IEnumerable<(string Name, FieldType Type, bool IsNullable)>> ReadableColumnsAsync(DatasetCreateInput input, CancellationToken cancellationToken)
    {
        if (input is DatasetCreateWithNewTableInput table)
        {
            return table.Columns.Select(column => (
                column.Name,
                new FieldType { DataType = column.DataType, NumericPrecision = column.NumericPrecision, NumericScale = column.NumericScale },
                column.IsNullable));
        }

        var existing = (DatasetCreateFromExistingInput)input;
        var columns = await sourceObjects.AllColumnsAsync(cancellationToken);
        return (columns.GetValueOrDefault(existing.SourceObject) ?? [])
            .Select(column => (Column: column, Type: ColumnTypeMapping.Map(column.ColumnType)))
            .Where(pair => pair.Type is not null)
            .Select(pair => (pair.Column.Name, pair.Type!, pair.Column.IsNullable));
    }

    /// <summary>
    /// Deletes a dataset that no frozen snapshot holds, with its live version and its content (D10), and drops
    /// the managed tables only it reads with their materialized views and registry rows (OI-33). The drops run first as the plan's
    /// schema statements.
    /// </summary>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="cancellationToken">Cancels the reads, statements and deletes.</param>
    /// <returns>Null when deleted; otherwise the blocked delete.</returns>
    public async Task<Refusal?> DeleteAsync(long datasetId, CancellationToken cancellationToken)
    {
        var dataset = await datasets.GetAsync(new { DatasetId = datasetId }, cancellationToken);
        var own = await versions.ListAsync(new { DatasetId = datasetId }, cancellationToken);
        var check = DatasetDeleteRules.Dataset(dataset, await holders.FrozenKeysAsync(own.Select(version => version.SnapshotId), cancellationToken));
        if (!check.Allowed) return Refusal.DeleteBlocked(check);
        long[] versionIds = [.. own.Select(version => version.DatasetVersionId)];
        var plan = new WritePlan();
        await tableDrops.PlanAsync(datasetId, own, plan, cancellationToken);
        plan.Call(WriteKind.Delete, 2, token => content.DeleteAsync(versionIds, token));
        plan.Delete(versions, 1, new { DatasetVersionId = versionIds });
        plan.Delete(datasets, WritePlan.Root, new { DatasetId = datasetId });
        await plan.ExecuteAsync(cancellationToken);
        await semanticRevision.AdvanceAsync(cancellationToken);
        return null;
    }

    /// <summary>Stores an enabled live version over a source object, supporting aggregate and detail queries.</summary>
    private async Task<DatasetVersionRecord> InsertLiveAsync(long datasetId, string kind, string sourceObject, CancellationToken cancellationToken)
    {
        var version = new DatasetVersionRecord
        {
            DatasetVersionId = ids.NextId(),
            DatasetId = datasetId,
            SnapshotId = await live.IdAsync(cancellationToken),
            IsEnabled = true,
            SourceObjectKind = kind,
            SourceObject = sourceObject,
            SupportsAggregate = true,
            SupportsDetail = true,
            ExpressionLanguageVersion = ExpressionLanguageVersion,
            Properties = "{}",
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        await versions.InsertAsync(version, cancellationToken);
        return version;
    }
}
