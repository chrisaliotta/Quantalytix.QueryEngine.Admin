using System.Diagnostics;
using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
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
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    ITableGateway<SemanticReleaseRecord> releases,
    ITableGateway<AssumptionSetRecord> assumptionSets,
    CalculationLookups calculations)
{
    /// <summary>Lists one picker. Items are sorted by key unless the kind has a natural order.</summary>
    /// <param name="kind">A <c>lookup_kind</c> value, already validated by <see cref="LookupQueryValidator"/>.</param>
    /// <param name="parentId">The owning row's ID; present for child kinds, optional for <c>mapping-sets</c>.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The picker's items.</returns>
    public Task<IReadOnlyList<LookupItem>> ListAsync(string kind, long? parentId, CancellationToken cancellationToken)
    {
        return kind switch
        {
            LookupKinds.Datasets => DatasetsAsync(cancellationToken),
            LookupKinds.DatasetVersions => DatasetVersionsAsync(parentId!.Value, cancellationToken),
            LookupKinds.Fields => FieldsAsync(parentId!.Value, cancellationToken),
            LookupKinds.Grains => GrainsAsync(parentId!.Value, cancellationToken),
            LookupKinds.Bindings => BindingsAsync(parentId!.Value, cancellationToken),
            LookupKinds.TimeBindings => TimeBindingsAsync(parentId!.Value, cancellationToken),
            LookupKinds.Measures => MeasuresAsync(parentId!.Value, cancellationToken),
            LookupKinds.Dimensions => DimensionsAsync(cancellationToken),
            LookupKinds.DimensionVersions => DimensionVersionsAsync(parentId!.Value, cancellationToken),
            LookupKinds.Levels => LevelsAsync(parentId!.Value, cancellationToken),
            LookupKinds.Members => MembersAsync(parentId!.Value, cancellationToken),
            LookupKinds.MappingSets => MappingSetsAsync(parentId, cancellationToken),
            LookupKinds.MappingVersions => MappingVersionsAsync(parentId!.Value, cancellationToken),
            LookupKinds.Bridges => BridgesAsync(cancellationToken),
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

    /// <summary>The dimension bindings of one dataset version, classified by binding kind. Bindings have no key, so their ID is it.</summary>
    private async Task<IReadOnlyList<LookupItem>> BindingsAsync(long datasetVersionId, CancellationToken cancellationToken)
    {
        var rows = await bindings.ListAsync(new { DatasetVersionId = datasetVersionId }, cancellationToken);
        return SortByKey(rows.Select(row => new LookupItem
        {
            Id = row.BindingId,
            Key = BindingKey(row.BindingId),
            Label = row.DisplayName,
            Classifier = row.BindingKind
        }));
    }

    /// <summary>The bindings of one dataset version that have a time extension, classified by time value type.</summary>
    private async Task<IReadOnlyList<LookupItem>> TimeBindingsAsync(long datasetVersionId, CancellationToken cancellationToken)
    {
        var rows = await bindings.ListAsync(new { DatasetVersionId = datasetVersionId }, cancellationToken);
        var extensions = await timeBindings.ListAsync(new { BindingId = rows.Select(row => row.BindingId).ToArray() }, cancellationToken);
        var timeValueTypes = extensions.ToDictionary(extension => extension.BindingId, extension => extension.TimeValueType);
        return SortByKey(rows.Where(row => timeValueTypes.ContainsKey(row.BindingId)).Select(row => new LookupItem
        {
            Id = row.BindingId,
            Key = BindingKey(row.BindingId),
            Label = row.DisplayName,
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

    /// <summary>Every canonical dimension, classified by dimension kind.</summary>
    private async Task<IReadOnlyList<LookupItem>> DimensionsAsync(CancellationToken cancellationToken)
    {
        var rows = await dimensions.ListAsync(null, cancellationToken);
        return SortByKey(rows.Select(row => new LookupItem
        {
            Id = row.DimensionId,
            Key = row.DimensionKey,
            Label = row.DisplayName,
            Classifier = row.DimensionKind
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

    /// <summary>Every bridge.</summary>
    private async Task<IReadOnlyList<LookupItem>> BridgesAsync(CancellationToken cancellationToken)
    {
        var rows = await bridges.ListAsync(null, cancellationToken);
        return SortByKey(rows.Select(row => new LookupItem { Id = row.BridgeId, Key = row.BridgeKey, Label = row.DisplayName }));
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
