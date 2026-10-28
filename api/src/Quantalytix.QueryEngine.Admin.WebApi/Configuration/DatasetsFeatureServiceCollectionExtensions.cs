using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Measures;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.MaterializedViews;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SchemaChanges;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SourceObjects;
using Quantalytix.QueryEngine.Admin.WebApi.Routing;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registration hook owned by the Datasets slice (datasets, versions, fields, grains, bindings, measures and source objects).</summary>
public static class DatasetsFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Datasets slice: its route resources (fields are found within the route's version), the StarRocks catalog,
    /// managed tables and materialized views, its reads and commands, the dataset version change-set aggregate with its snapshot
    /// rules in the order they report (blueprint 11, Rule codes), the same rules as the workflow's <c>dataset_version</c>
    /// publication rule, and its retirement rule.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDatasetsFeature(this IServiceCollection services)
    {
        services.AddRouteResource<DatasetRecord>("datasets", id => new { DatasetId = id });
        services.AddRouteResource<DatasetVersionRecord>("dataset-versions", id => new { DatasetVersionId = id });
        services.AddRouteResource<FieldRecord>("fields", (value, route) => new { FieldId = Id(value), DatasetVersionId = Id(route["versionId"]) });

        services.AddSingleton(typeof(SourceReadCache<>));
        services.AddScoped<ISourceObjectCatalog, SourceObjectCatalog>();
        services.AddScoped<IManagedTables, StarRocksManagedTables>();
        services.AddScoped<IMaterializedViews, StarRocksMaterializedViews>();

        services.AddScoped<SourceObjectDirectory>();
        services.AddScoped<DatasetReleaseReader>();
        services.AddScoped<DatasetSnapshotLoader>();
        services.AddScoped<DatasetVersionReader>();
        services.AddScoped<DatasetVersionContentStore>();
        services.AddScoped<DatasetVersionCloner>();
        services.AddScoped<DatasetQueries>();
        services.AddScoped<DatasetCommands>();
        services.AddScoped<DatasetChanges>();
        services.AddScoped<DatasetSchemaChanges>();
        services.AddScoped<DatasetMaterializations>();
        services.AddScoped<DatasetFieldValues>();

        services.AddChangeSetAggregate<DatasetAggregate, DatasetSnapshot, DatasetOperation, DatasetVersion>();
        services.AddSnapshotRule<DatasetSnapshot, SourceObjectExistsRule>();
        services.AddSnapshotRule<DatasetSnapshot, VersionSupportsAModeRule>();
        services.AddSnapshotRule<DatasetSnapshot, DefaultTimeBindingInVersionRule>();
        services.AddSnapshotRule<DatasetSnapshot, FieldKeyUniqueRule>();
        services.AddSnapshotRule<DatasetSnapshot, FieldColumnsExistRule>();
        services.AddSnapshotRule<DatasetSnapshot, ExpressionFieldReferencesRule>();
        services.AddSnapshotRule<DatasetSnapshot, ExpressionFieldAcyclicRule>();
        services.AddSnapshotRule<DatasetSnapshot, GrainKeyUniqueRule>();
        services.AddSnapshotRule<DatasetSnapshot, GrainFieldsInVersionRule>();
        services.AddSnapshotRule<DatasetSnapshot, SeriesGrainExcludesTimeFieldRule>();
        services.AddSnapshotRule<DatasetSnapshot, BindingFieldsInVersionRule>();
        services.AddSnapshotRule<DatasetSnapshot, BindingSourceFieldGroupableRule>();
        services.AddSnapshotRule<DatasetSnapshot, BoundRolesDistinctRule>();
        services.AddSnapshotRule<DatasetSnapshot, MappedBindingMappingSetDimensionRule>();
        services.AddSnapshotRule<DatasetSnapshot, RangeBindingDimensionKindRule>();
        services.AddSnapshotRule<DatasetSnapshot, TimeBindingDimensionKindRule>();
        services.AddSnapshotRule<DatasetSnapshot, BridgeBindingRouteRule>();
        services.AddSnapshotRule<DatasetSnapshot, UnknownMembersInVersionRule>();
        services.AddSnapshotRule<DatasetSnapshot, IntervalEstimatesForFinerPeriodsRule>();
        services.AddSnapshotRule<DatasetSnapshot, MeasureKeyUniqueRule>();
        services.AddSnapshotRule<DatasetSnapshot, ComponentFieldsInVersionRule>();
        services.AddSnapshotRule<DatasetSnapshot, ComponentRolesMatchKindRule>();
        services.AddSnapshotRule<DatasetSnapshot, FormulaReferencesKnownMeasuresRule>();
        services.AddSnapshotRule<DatasetSnapshot, FormulaAcyclicRule>();
        services.AddSnapshotRule<DatasetSnapshot, TimePolicySeriesGrainRule>();
        services.AddSnapshotRule<DatasetSnapshot, TimePolicyTimeBindingInVersionRule>();
        services.AddScoped<DatasetRules>();
        services.AddScoped<IPublicationRule, DatasetVersionRules>();
        services.AddScoped<IRetirementRule, ReleasePinsRetiredVersionRule>();
        return services;
    }

    /// <summary>A route id, which the route template constrains to digits.</summary>
    private static long Id(object? value)
    {
        return long.Parse((string)value!, CultureInfo.InvariantCulture);
    }
}
