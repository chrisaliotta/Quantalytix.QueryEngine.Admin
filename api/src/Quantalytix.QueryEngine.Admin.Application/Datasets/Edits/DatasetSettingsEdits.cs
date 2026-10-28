using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// The identity, settings and rename operations of the dataset change set. A rename moves a managed table (OI-33) only while no
/// other version reads it, so no published query ever read the old name; the commit runs the rename's DDL before the rows.
/// </summary>
internal static class DatasetSettingsEdits
{
    /// <summary><c>updateDataset</c>: an absent patch field keeps its value; the key is fixed (OI-36).</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The aggregate with the identity changed.</returns>
    public static DatasetSnapshot UpdateDataset(DatasetSnapshot snapshot, DatasetUpdateDatasetOperation operation)
    {
        var patch = operation.Patch;
        return snapshot with
        {
            Dataset = snapshot.Dataset with
            {
                DisplayName = patch.DisplayName ?? snapshot.Dataset.DisplayName,
                IsEnabled = patch.IsEnabled ?? snapshot.Dataset.IsEnabled
            }
        };
    }

    /// <summary><c>saveSettings</c>: the source object must exist; the default time binding must be a time binding of the version.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the settings replaced.</returns>
    public static DatasetSnapshot SaveSettings(DatasetSnapshot snapshot, DatasetSaveSettingsOperation operation, ChangeContext context)
    {
        var settings = operation.Settings;
        if (!snapshot.Surroundings.SourceObjects.Any(item => item.Kind == settings.SourceObjectKind && item.Name == settings.SourceObject))
            context.Reject("settings.sourceObject", $"'{settings.SourceObject}' is not a {settings.SourceObjectKind} of the tenant database.");
        var defaultTimeBindingId = DatasetEditTargets.Optional(settings.DefaultTimeBindingId, context);
        if (defaultTimeBindingId is { } bindingId && snapshot.TimeBindings.All(time => time.DimensionBindingId != bindingId))
            context.Reject("settings.defaultTimeBindingId", "Choose a time binding of this version.");
        if (context.Refused) return snapshot;

        return snapshot with
        {
            Version = snapshot.Version with
            {
                SourceObjectKind = settings.SourceObjectKind,
                SourceObject = settings.SourceObject,
                SupportsAggregate = settings.SupportsAggregate,
                SupportsDetail = settings.SupportsDetail,
                DefaultTimeBindingId = defaultTimeBindingId,
                ExpressionLanguageVersion = settings.ExpressionLanguageVersion,
                SecurityPolicyKey = string.IsNullOrWhiteSpace(settings.SecurityPolicyKey) ? null : settings.SecurityPolicyKey.Trim(),
                Properties = string.IsNullOrWhiteSpace(settings.Properties) ? "{}" : settings.Properties.Trim()
            }
        };
    }

    /// <summary>
    /// <c>renameSourceTable</c>: renames the managed table the version reads, in the registry and the physical catalog. A table the
    /// database already holds under the new name, and no longer under the old one, is this rename's own DDL, which the commit runs
    /// before it replays the operations.
    /// </summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Takes refusals.</param>
    /// <returns>The aggregate reading the renamed table.</returns>
    public static DatasetSnapshot RenameSourceTable(DatasetSnapshot snapshot, DatasetRenameSourceTableOperation operation, ChangeContext context)
    {
        var from = snapshot.Version.SourceObject;
        var to = operation.TableName;
        if (to == from) return snapshot;
        if (snapshot.ManagedSource is not { SourceObjectKind: SourceObjectKinds.Table } managed)
        {
            context.Conflict(DatasetRefusals.NotManagedTitle, $"'{from}' was not created by the API, so it cannot be renamed.");
            return snapshot;
        }

        var readers = snapshot.Surroundings.TenantVersions
            .Where(version => version.SourceObject == from && version.SourceObjectKind == SourceObjectKinds.Table && version.DatasetVersionId != snapshot.VersionId)
            .ToList();
        if (readers.Count > 0)
        {
            context.Conflict("Table is read by other versions", $"{readers.Count} other dataset versions read '{from}', so it keeps its name.");
            return snapshot;
        }

        var surroundings = snapshot.Surroundings;
        var renamed = !surroundings.SourceObjects.Any(item => item.Name == from) && surroundings.SourceObjects.Any(item => item.Name == to);
        if (!renamed && surroundings.SourceObjects.Any(item => item.Name == to))
        {
            context.Reject("tableName", $"'{to}' already exists in the tenant database.");
            return snapshot;
        }

        var moved = snapshot with
        {
            Version = snapshot.Version with { SourceObject = to },
            ManagedObjects = [.. snapshot.ManagedObjects.Select(row => row == managed ? row with { SourceObject = to } : row)]
        };
        if (renamed) return moved;
        return moved with
        {
            Surroundings = surroundings with
            {
                SourceObjects = [.. surroundings.SourceObjects.Select(item => item.Name == from ? item with { Name = to } : item)],
                Columns = surroundings.Columns.ToDictionary(entry => entry.Key == from ? to : entry.Key, entry => entry.Value, StringComparer.Ordinal)
            }
        };
    }

    /// <summary>The snapshot with a column the commit adds to its source table.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="column">The new column.</param>
    /// <returns>The aggregate whose physical catalog has the column.</returns>
    public static DatasetSnapshot WithColumn(DatasetSnapshot snapshot, PhysicalColumn column)
    {
        var columns = new Dictionary<string, IReadOnlyList<PhysicalColumn>>(snapshot.Surroundings.Columns, StringComparer.Ordinal)
        {
            [snapshot.Version.SourceObject] = [.. snapshot.SourceColumns, column]
        };
        return snapshot with { Surroundings = snapshot.Surroundings with { Columns = columns } };
    }
}
