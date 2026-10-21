using System.Diagnostics;
using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Lookups;

/// <summary>
/// Serves every picker list of the web app from the tenant's metadata tables, so each feature can offer references to
/// another feature's rows without depending on it. <see cref="CalculationLookups"/> serves the calculation and report kinds.
/// </summary>
public sealed class LookupService(
    ITableGateway<DatasetRecord> datasets,
    ITableGateway<DatasetVersionRecord> datasetVersions,
    ITableGateway<FieldRecord> fields,
    ITableGateway<DatasetGrainRecord> grains,
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<TimeBindingRecord> timeBindings,
    ITableGateway<MeasureRecord> measures,
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    ITableGateway<SemanticReleaseRecord> releases,
    ITableGateway<AssumptionSetRecord> assumptionSets,
    BindingNames bindingNames,
    CalculationLookups calculations)
{
    /// <summary>Lists one picker. Items are sorted by key unless the kind has a natural order.</summary>
    /// <param name="query">
    /// The <c>lookup_kind</c> value, already validated by <see cref="LookupQueryValidator"/>; the owning row's ID, present for child
    /// kinds and optional for <c>dimension-roles</c>, <c>mapping-sets</c> and <c>bridges</c>; and the purpose that narrows <c>dimensions</c>.
    /// </param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The picker's items.</returns>
    public Task<IReadOnlyList<LookupItem>> ListAsync(LookupQuery query, CancellationToken cancellationToken)
    {
        var kind = query.Kind;
        var parentId = query.ParentId;
        return kind switch
        {
            LookupKinds.Datasets => DatasetsAsync(cancellationToken),
            LookupKinds.DatasetVersions => DatasetVersionsAsync(parentId!.Value, cancellationToken),
            LookupKinds.Fields => FieldsAsync(parentId!.Value, cancellationToken),
            LookupKinds.BoundRoles => BoundRolesAsync(parentId!.Value, cancellationToken),
            LookupKinds.Grains => GrainsAsync(parentId!.Value, cancellationToken),
            LookupKinds.Bindings => BindingsAsync(parentId!.Value, cancellationToken),
            LookupKinds.TimeBindings => TimeBindingsAsync(parentId!.Value, cancellationToken),
            LookupKinds.Measures => MeasuresAsync(parentId!.Value, cancellationToken),
            LookupKinds.Dimensions => DimensionsAsync(query.Purpose, cancellationToken),
            LookupKinds.DimensionRoles => DimensionRolesAsync(parentId, cancellationToken),
            LookupKinds.DimensionVersions => DimensionVersionsAsync(parentId!.Value, cancellationToken),
            LookupKinds.Levels => LevelsAsync(parentId!.Value, cancellationToken),
            LookupKinds.Members => MembersAsync(parentId!.Value, cancellationToken),
            LookupKinds.MappingSets => MappingSetsAsync(parentId, cancellationToken),
            LookupKinds.MappingVersions => MappingVersionsAsync(parentId!.Value, cancellationToken),
            LookupKinds.Bridges => BridgesAsync(parentId, cancellationToken),
            LookupKinds.BridgeVersions => BridgeVersionsAsync(parentId!.Value, cancellationToken),
            LookupKinds.Releases => ReleasesAsync(cancellationToken),
            LookupKinds.AssumptionSets => AssumptionSetsAsync(cancellationToken),
            LookupKinds.CalculationModels or LookupKinds.ModelVersions or LookupKinds.Variables or LookupKinds.VariableSources
                or LookupKinds.SourceProfiles or LookupKinds.CalculatedMeasures or LookupKinds.CalculationDimensions or LookupKinds.Reports
                or LookupKinds.ReportVersions or LookupKinds.ReportColumns => calculations.ListAsync(kind, parentId, cancellationToken),
            _ => throw new UnreachableException($"Lookup kind '{kind}' passed validation but has no list.")
        };
    }

    /// <summary>Every dataset.</summary>
    private async Task<IReadOnlyList<LookupItem>> DatasetsAsync(CancellationToken cancellationToken)
    {
        var rows = await datasets.ListAsync(null, cancellationToken);
        return SortByKey(rows.Select(row => new LookupItem { Id = row.DatasetId, Key = row.DatasetKey, Label = row.DisplayName }));
    }

    /// <summary>The versions of one dataset, by version number.</summary>
    private async Task<IReadOnlyList<LookupItem>> DatasetVersionsAsync(long datasetId, CancellationToken cancellationToken)
    {
        var rows = await datasetVersions.ListAsync(new { DatasetId = datasetId }, cancellationToken);
        return
        [
            .. rows.OrderBy(row => row.VersionNumber).Select(row => new LookupItem
            {
                Id = row.DatasetVersionId,
                Key = VersionKey(row.VersionNumber),
                Label = $"{VersionKey(row.VersionNumber)} · {row.SourceObject}",
                Status = row.Status
            })
        ];
    }

    /// <summary>The fields of one dataset version, classified by data type.</summary>
    private async Task<IReadOnlyList<LookupItem>> FieldsAsync(long datasetVersionId, CancellationToken cancellationToken)
    {
        var rows = await fields.ListAsync(new { DatasetVersionId = datasetVersionId }, cancellationToken);
        return SortByKey(rows.Select(row => new LookupItem
        {
            Id = row.FieldId,
            Key = row.FieldKey,
            Label = row.DisplayName,
            Classifier = row.DataType
        }));
    }

    /// <summary>
    /// The roles one dataset version binds into members with an enabled binding, each pointing at its template and classified by
    /// binding kind: the roles a bridge classification can start from.
    /// </summary>
    private async Task<IReadOnlyList<LookupItem>> BoundRolesAsync(long datasetVersionId, CancellationToken cancellationToken)
    {
        var feeding = (await bindings.ListAsync(new { DatasetVersionId = datasetVersionId }, cancellationToken))
            .Where(ClassificationFields.FeedsBridges)
            .DistinctBy(binding => binding.RoleId)
            .ToList();
        var bound = (await roles.ListAsync(new { RoleId = feeding.Select(binding => binding.RoleId!.Value).ToArray() }, cancellationToken))
            .ToDictionary(role => role.RoleId);
        return SortByKey(feeding.Select(binding =>
        {
            var role = bound[binding.RoleId!.Value];
            return new LookupItem { Id = role.RoleId, Key = role.RoleKey, Label = role.DisplayName, ParentId = role.DimensionId, Classifier = binding.BindingKind };
        }));
    }

    /// <summary>The grains of one dataset version, classified by grain kind.</summary>
    private async Task<IReadOnlyList<LookupItem>> GrainsAsync(long datasetVersionId, CancellationToken cancellationToken)
    {
        var rows = await grains.ListAsync(new { DatasetVersionId = datasetVersionId }, cancellationToken);
        return SortByKey(rows.Select(row => new LookupItem
        {
            Id = row.GrainId,
            Key = row.GrainKey,
            Label = row.GrainKey,
            Classifier = row.GrainKind
        }));
    }

    /// <summary>
    /// The dimension bindings of one dataset version, labeled by the role they bind or a time binding's field, and classified by
    /// binding kind. Bindings have no key, so their ID is it.
    /// </summary>
    private async Task<IReadOnlyList<LookupItem>> BindingsAsync(long datasetVersionId, CancellationToken cancellationToken)
    {
        var rows = await bindings.ListAsync(new { DatasetVersionId = datasetVersionId }, cancellationToken);
        var names = await bindingNames.OfAsync(rows, cancellationToken);
        return SortByKey(rows.Select(row => new LookupItem
        {
            Id = row.BindingId,
            Key = BindingKey(row.BindingId),
            Label = names[row.BindingId],
            Classifier = row.BindingKind
        }));
    }

    /// <summary>The bindings of one dataset version that have a time extension, labeled by their field and classified by time value type.</summary>
    private async Task<IReadOnlyList<LookupItem>> TimeBindingsAsync(long datasetVersionId, CancellationToken cancellationToken)
    {
        var rows = await bindings.ListAsync(new { DatasetVersionId = datasetVersionId }, cancellationToken);
        var extensions = await timeBindings.ListAsync(new { BindingId = rows.Select(row => row.BindingId).ToArray() }, cancellationToken);
        var timeValueTypes = extensions.ToDictionary(extension => extension.BindingId, extension => extension.TimeValueType);
        var timed = rows.Where(row => timeValueTypes.ContainsKey(row.BindingId)).ToList();
        var names = await bindingNames.OfAsync(timed, cancellationToken);
        return SortByKey(timed.Select(row => new LookupItem
        {
            Id = row.BindingId,
            Key = BindingKey(row.BindingId),
            Label = names[row.BindingId],
            Classifier = timeValueTypes[row.BindingId]
        }));
    }

    /// <summary>The measures of one dataset version, classified by measure kind.</summary>
    private async Task<IReadOnlyList<LookupItem>> MeasuresAsync(long datasetVersionId, CancellationToken cancellationToken)
    {
        var rows = await measures.ListAsync(new { DatasetVersionId = datasetVersionId }, cancellationToken);
        return SortByKey(rows.Select(row => new LookupItem
        {
            Id = row.MeasureId,
            Key = row.MeasureKey,
            Label = row.DisplayName,
            Classifier = row.MeasureKind
        }));
    }

    /// <summary>Every canonical dimension, or every dimension of one purpose, classified by dimension kind.</summary>
    private async Task<IReadOnlyList<LookupItem>> DimensionsAsync(string? purpose, CancellationToken cancellationToken)
    {
        object? filter = null;
        if (purpose is not null) filter = new { DimensionPurpose = purpose };
        var rows = await dimensions.ListAsync(filter, cancellationToken);
        return SortByKey(rows.Select(row => new LookupItem
        {
            Id = row.DimensionId,
            Key = row.DimensionKey,
            Label = row.DisplayName,
            Classifier = row.DimensionKind
        }));
    }

    /// <summary>
    /// The roles of one dimension, or of every dimension, labeled like <c>Servicing branch · Branch</c>, each pointing at its
    /// template and classified by the template's kind.
    /// </summary>
    private async Task<IReadOnlyList<LookupItem>> DimensionRolesAsync(long? dimensionId, CancellationToken cancellationToken)
    {
        object? filter = null;
        if (dimensionId is not null) filter = new { DimensionId = dimensionId.Value };
        var rows = await roles.ListAsync(filter, cancellationToken);
        var templates = (await dimensions.ListAsync(new { DimensionId = rows.Select(row => row.DimensionId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(dimension => dimension.DimensionId);
        return SortByKey(rows.Select(row => new LookupItem
        {
            Id = row.RoleId,
            Key = row.RoleKey,
            Label = $"{row.DisplayName} · {templates[row.DimensionId].DisplayName}",
            ParentId = row.DimensionId,
            Classifier = templates[row.DimensionId].DimensionKind
        }));
    }

    /// <summary>The versions of one dimension by version number.</summary>
    private async Task<IReadOnlyList<LookupItem>> DimensionVersionsAsync(long dimensionId, CancellationToken cancellationToken)
    {
        var rows = await dimensionVersions.ListAsync(new { DimensionId = dimensionId }, cancellationToken);
        return
        [
            .. rows.OrderBy(row => row.VersionNumber).Select(row => new LookupItem
            {
                Id = row.DimensionVersionId,
                Key = VersionKey(row.VersionNumber),
                Label = VersionKey(row.VersionNumber),
                Status = row.Status,
                Classifier = row.RangeDataType
            })
        ];
    }

    /// <summary>The levels of one dimension version, by ordinal and classified by periodicity.</summary>
    private async Task<IReadOnlyList<LookupItem>> LevelsAsync(long dimensionVersionId, CancellationToken cancellationToken)
    {
        var rows = await levels.ListAsync(new { DimensionVersionId = dimensionVersionId }, cancellationToken);
        return
        [
            .. rows.OrderBy(row => row.Ordinal).Select(row => new LookupItem
            {
                Id = row.LevelId,
                Key = row.LevelKey,
                Label = row.DisplayName,
                Classifier = row.Periodicity
            })
        ];
    }

    /// <summary>The members of one dimension version by sort order, each pointing at its parent member.</summary>
    private async Task<IReadOnlyList<LookupItem>> MembersAsync(long dimensionVersionId, CancellationToken cancellationToken)
    {
        var rows = await members.ListAsync(new { DimensionVersionId = dimensionVersionId }, cancellationToken);
        return
        [
            .. rows
                .OrderBy(row => row.SortOrder)
                .ThenBy(row => row.MemberKey, StringComparer.OrdinalIgnoreCase)
                .Select(row => new LookupItem
                {
                    Id = row.MemberId,
                    Key = row.MemberKey,
                    Label = row.DisplayName,
                    ParentId = row.ParentMemberId
                })
        ];
    }

    /// <summary>The mapping sets of one dimension, or of every dimension, each pointing at its dimension.</summary>
    private async Task<IReadOnlyList<LookupItem>> MappingSetsAsync(long? dimensionId, CancellationToken cancellationToken)
    {
        object? filter = null;
        if (dimensionId is not null) filter = new { DimensionId = dimensionId.Value };
        var rows = await mappingSets.ListAsync(filter, cancellationToken);
        return SortByKey(rows.Select(row => new LookupItem
        {
            Id = row.MappingSetId,
            Key = row.MappingSetKey,
            Label = row.DisplayName,
            ParentId = row.DimensionId,
            Classifier = row.SourceNamespace
        }));
    }

    /// <summary>The versions of one mapping set by version number.</summary>
    private async Task<IReadOnlyList<LookupItem>> MappingVersionsAsync(long mappingSetId, CancellationToken cancellationToken)
    {
        var rows = await mappingVersions.ListAsync(new { MappingSetId = mappingSetId }, cancellationToken);
        return
        [
            .. rows.OrderBy(row => row.VersionNumber).Select(row => new LookupItem
            {
                Id = row.MappingVersionId,
                Key = VersionKey(row.VersionNumber),
                Label = VersionKey(row.VersionNumber),
                Status = row.Status
            })
        ];
    }

    /// <summary>The bridges from one source dimension, or every bridge, each pointing at its target dimension.</summary>
    private async Task<IReadOnlyList<LookupItem>> BridgesAsync(long? sourceDimensionId, CancellationToken cancellationToken)
    {
        object? filter = null;
        if (sourceDimensionId is not null) filter = new { SourceDimensionId = sourceDimensionId.Value };
        var rows = await bridges.ListAsync(filter, cancellationToken);
        return SortByKey(rows.Select(row => new LookupItem { Id = row.BridgeId, Key = row.BridgeKey, Label = row.DisplayName, ParentId = row.TargetDimensionId }));
    }

    /// <summary>The versions of one bridge by version number, classified by allocation mode.</summary>
    private async Task<IReadOnlyList<LookupItem>> BridgeVersionsAsync(long bridgeId, CancellationToken cancellationToken)
    {
        var rows = await bridgeVersions.ListAsync(new { BridgeId = bridgeId }, cancellationToken);
        return
        [
            .. rows.OrderBy(row => row.VersionNumber).Select(row => new LookupItem
            {
                Id = row.BridgeVersionId,
                Key = VersionKey(row.VersionNumber),
                Label = VersionKey(row.VersionNumber),
                Status = row.Status,
                Classifier = row.AllocationMode
            })
        ];
    }

    /// <summary>Every semantic release.</summary>
    private async Task<IReadOnlyList<LookupItem>> ReleasesAsync(CancellationToken cancellationToken)
    {
        var rows = await releases.ListAsync(null, cancellationToken);
        return SortByKey(rows.Select(row => new LookupItem
        {
            Id = row.ReleaseId,
            Key = row.ReleaseKey,
            Label = row.ReleaseKey,
            Status = row.Status
        }));
    }

    /// <summary>Every assumption set: the active one, then historical sets by key.</summary>
    private async Task<IReadOnlyList<LookupItem>> AssumptionSetsAsync(CancellationToken cancellationToken)
    {
        var rows = await assumptionSets.ListAsync(null, cancellationToken);
        return
        [
            .. rows
                .OrderBy(row => row.Status != AssumptionSetStatuses.Active)
                .ThenBy(row => row.AssumptionSetKey, StringComparer.OrdinalIgnoreCase)
                .Select(row => new LookupItem { Id = row.AssumptionSetId, Key = row.AssumptionSetKey, Label = row.DisplayName, Status = row.Status })
        ];
    }

    /// <summary>Orders items by key, ignoring case.</summary>
    private static IReadOnlyList<LookupItem> SortByKey(IEnumerable<LookupItem> items)
    {
        return [.. items.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The key of a version: <c>v</c> followed by its number.</summary>
    private static string VersionKey(int versionNumber)
    {
        return string.Create(CultureInfo.InvariantCulture, $"v{versionNumber}");
    }

    /// <summary>The key of a binding, which has no key column: its ID as text.</summary>
    private static string BindingKey(long bindingId)
    {
        return bindingId.ToString(CultureInfo.InvariantCulture);
    }
}
