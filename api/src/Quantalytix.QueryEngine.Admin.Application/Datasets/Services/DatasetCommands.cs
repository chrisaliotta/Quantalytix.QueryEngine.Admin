using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The dataset manager's lifecycle commands, which run on stored state, never inside a change set: create a dataset over an existing
/// object or a new managed table (OI-33), delete it, create and delete drafts (one open version per dataset, OI-23; OI-17), and the
/// strict transitions validate, publish, reopen and retire (D1) through the version workflow, which runs the publication rules on
/// leaving draft and the retirement rules on retiring.
/// </summary>
public sealed class DatasetCommands(
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> versions,
    ITableGateway<FieldRecord> fields,
    ITableGateway<ManagedSourceObjectRecord> managed,
    DatasetVersionContentStore content,
    DatasetVersionCloner cloner,
    DatasetReleaseReader releases,
    DatasetQueries queries,
    SourceObjectDirectory sourceObjects,
    IManagedTables managedTables,
    VersionWorkflow workflow,
    IIdGenerator ids,
    TimeProvider time)
{
    /// <summary>The expression grammar version of a new version: the one the query engine parses.</summary>
    private const string ExpressionLanguageVersion = "1.0";

    /// <summary>
    /// Creates a dataset with draft version 1 whose fields read every column fields can read. A new managed table is created and
    /// registered first: DDL cannot be rolled back, and a table without a dataset is a residue the registry still names.
    /// </summary>
    /// <param name="input">The new dataset, already validated.</param>
    /// <param name="cancellationToken">Cancels the reads, statements and writes.</param>
    /// <returns>The list item, or a conflict when the key or the table name is taken.</returns>
    public async Task<Outcome<DatasetListItem>> CreateAsync(DatasetCreateInput input, CancellationToken cancellationToken)
    {
        if ((await datasets.ListAsync(null, cancellationToken)).Any(row => string.Equals(row.DatasetKey, input.Key, StringComparison.OrdinalIgnoreCase)))
            return Outcome<DatasetListItem>.Refused(Refusal.Conflict("Key taken", $"Another dataset already uses the key '{input.Key}'."));

        var dataset = new DatasetRecord { DatasetId = ids.NextId(), DatasetKey = input.Key, DisplayName = input.DisplayName.Trim(), IsEnabled = true };
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
        var version = await InsertDraftAsync(dataset.DatasetId, 1, kind, sourceObject, cancellationToken);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        await fields.InsertManyAsync(
            [
                .. (await ReadableColumnsAsync(input, cancellationToken))
                    .Select(column => ColumnFields.Of(ids.NextId(), version.DatasetVersionId, ColumnFields.KeyOf(column.Name, keys), column.Name, column.Type, column.IsNullable))
            ],
            cancellationToken);
        return Outcome<DatasetListItem>.Success(await queries.GetAsync(dataset.DatasetId, cancellationToken));
    }

    /// <summary>The columns draft version 1 gets a field for: a new table's requested columns, or every readable column of an existing object.</summary>
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

    /// <summary>Deletes a dataset whose versions are all open and that no release lists, with every version and its content (D10).</summary>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when deleted; otherwise the blocked delete.</returns>
    public async Task<Refusal?> DeleteAsync(long datasetId, CancellationToken cancellationToken)
    {
        var dataset = await datasets.GetAsync(new { DatasetId = datasetId }, cancellationToken);
        var own = await versions.ListAsync(new { DatasetId = datasetId }, cancellationToken);
        var check = DatasetDeleteRules.Dataset(dataset, own, await releases.ReadAsync(datasetId, cancellationToken));
        if (!check.Allowed) return Refusal.DeleteBlocked(check);
        long[] versionIds = [.. own.Select(version => version.DatasetVersionId)];
        await content.DeleteAsync(versionIds, cancellationToken);
        await versions.DeleteAsync(new { DatasetVersionId = versionIds }, cancellationToken);
        await datasets.DeleteAsync(new { DatasetId = datasetId }, cancellationToken);
        return null;
    }

    /// <summary>Creates a draft with the next version number, empty or cloned from another version of the dataset (OI-12).</summary>
    /// <param name="datasetId">The dataset.</param>
    /// <param name="input">The draft's source object, or the version to clone.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The draft's summary, a conflict when the dataset has an open version (OI-23), or an invalid clone source.</returns>
    public async Task<Outcome<DatasetVersionSummary>> CreateDraftAsync(long datasetId, DatasetCreateDraftInput input, CancellationToken cancellationToken)
    {
        var own = await versions.ListAsync(new { DatasetId = datasetId }, cancellationToken);
        if (own.FirstOrDefault(DatasetDeleteRules.IsOpen) is { } open)
            return Outcome<DatasetVersionSummary>.Refused(Refusal.Conflict("An open version exists", $"Version v{open.VersionNumber} is {open.Status}; publish or delete it before starting another."));

        var versionNumber = VersionNumbers.Next(own.Select(version => version.VersionNumber));
        DatasetVersionRecord draft;
        if (input is DatasetCloneDraftInput clone)
        {
            if (own.All(version => version.DatasetVersionId != clone.FromVersionId))
                return Outcome<DatasetVersionSummary>.Refused(Refusal.Invalid(new Dictionary<string, string[]> { ["fromVersionId"] = ["Choose a version of this dataset."] }));
            draft = await cloner.CloneAsync(clone.FromVersionId, datasetId, versionNumber, cancellationToken);
        }
        else
        {
            var empty = (DatasetCreateEmptyDraftInput)input;
            draft = await InsertDraftAsync(datasetId, versionNumber, empty.SourceObjectKind, empty.SourceObject, cancellationToken);
        }

        return Outcome<DatasetVersionSummary>.Success(DatasetReadModels.Summary(draft, await releases.ReadAsync(datasetId, cancellationToken)));
    }

    /// <summary>Deletes an open version that no release pins, with its content (OI-17).</summary>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when deleted; otherwise the blocked delete.</returns>
    public async Task<Refusal?> DeleteVersionAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var check = DatasetDeleteRules.Version(version, await releases.ReadAsync(version.DatasetId, cancellationToken));
        if (!check.Allowed) return Refusal.DeleteBlocked(check);
        await content.DeleteAsync([versionId], cancellationToken);
        await versions.DeleteAsync(new { DatasetVersionId = versionId }, cancellationToken);
        return null;
    }

    /// <summary>Moves a draft to validated when every rule passes.</summary>
    /// <param name="versionId">The draft.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The validated version, a conflict when it is not a draft, or the broken rules.</returns>
    public Task<Outcome<DatasetVersionSummary>> ValidateAsync(long versionId, CancellationToken cancellationToken)
    {
        return StepAsync(versionId, [PublicationStatuses.Draft], "a draft", [PublicationStatuses.Validated], cancellationToken);
    }

    /// <summary>Publishes a validated version, validating a draft first in the same call.</summary>
    /// <param name="versionId">The draft or validated version.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The published version, a conflict when it is neither, or the broken rules with nothing written.</returns>
    public async Task<Outcome<DatasetVersionSummary>> PublishAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DatasetVersionId = versionId }, cancellationToken);
        string[] path = version.Status == PublicationStatuses.Draft ? [PublicationStatuses.Validated, PublicationStatuses.Published] : [PublicationStatuses.Published];
        return await StepAsync(versionId, [PublicationStatuses.Draft, PublicationStatuses.Validated], "a draft or validated version", path, cancellationToken);
    }

    /// <summary>Returns a validated version to draft.</summary>
    /// <param name="versionId">The validated version.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The draft, or a conflict when it is not validated.</returns>
    public Task<Outcome<DatasetVersionSummary>> ReopenAsync(long versionId, CancellationToken cancellationToken)
    {
        return StepAsync(versionId, [PublicationStatuses.Validated], "a validated version", [PublicationStatuses.Draft], cancellationToken);
    }

    /// <summary>Retires a published version that the active release does not pin.</summary>
    /// <param name="versionId">The published version.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The retired version, a conflict when it is not published, or the retirement rule it breaks.</returns>
    public Task<Outcome<DatasetVersionSummary>> RetireAsync(long versionId, CancellationToken cancellationToken)
    {
        return StepAsync(versionId, [PublicationStatuses.Published], "a published version", [PublicationStatuses.Retired], cancellationToken);
    }

    /// <summary>Moves a version through each status of a path, stopping at the first transition the rules reject.</summary>
    private async Task<Outcome<DatasetVersionSummary>> StepAsync(
        long versionId,
        string[] from,
        string allowed,
        string[] path,
        CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DatasetVersionId = versionId }, cancellationToken);
        if (!from.Contains(version.Status)) return Outcome<DatasetVersionSummary>.Refused(DatasetRefusals.WrongStatus(version, allowed));
        foreach (var status in path)
        {
            var result = await workflow.TransitionAsync(VersionKinds.DatasetVersion, versionId, status, cancellationToken);
            if (result.Outcome == TransitionOutcome.Rejected)
                return Outcome<DatasetVersionSummary>.Refused(Refusal.RulesViolated($"Version v{version.VersionNumber} breaks rules; its status is unchanged.", result.Violations));
        }

        var stepped = await versions.GetAsync(new { DatasetVersionId = versionId }, cancellationToken);
        return Outcome<DatasetVersionSummary>.Success(DatasetReadModels.Summary(stepped, await releases.ReadAsync(stepped.DatasetId, cancellationToken)));
    }

    /// <summary>Stores an empty draft over a source object, supporting aggregate and detail queries.</summary>
    private async Task<DatasetVersionRecord> InsertDraftAsync(long datasetId, int versionNumber, string kind, string sourceObject, CancellationToken cancellationToken)
    {
        var draft = new DatasetVersionRecord
        {
            DatasetVersionId = ids.NextId(),
            DatasetId = datasetId,
            VersionNumber = versionNumber,
            Status = PublicationStatuses.Draft,
            SourceObjectKind = kind,
            SourceObject = sourceObject,
            SupportsAggregate = true,
            SupportsDetail = true,
            ExpressionLanguageVersion = ExpressionLanguageVersion,
            Properties = "{}",
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        await versions.InsertAsync(draft, cancellationToken);
        return draft;
    }
}
