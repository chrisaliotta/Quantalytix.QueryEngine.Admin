using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SchemaChanges;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.WebApi.Routing;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registration hook owned by the Datasets slice (datasets, dataset versions, fields and grains).</summary>
public static class DatasetsFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the source object catalog and schema change executor, the dataset services, and the dataset version
    /// publication rules for versions, fields and grains, all scoped to the request.
    /// Also registers the slice's route resources, which the shared 404 check reads.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDatasetsFeature(this IServiceCollection services)
    {
        services.AddScoped<ISourceObjectCatalog, SourceObjectCatalog>();
        services.AddScoped<ISchemaChangeExecutor, SchemaChangeExecutor>();
        services.AddRouteResource<DatasetRecord>("datasets", id => new { DatasetId = id });
        services.AddRouteResource<DatasetVersionRecord>("dataset-versions", id => new { DatasetVersionId = id });
        services.AddRouteResource<FieldRecord>("fields", id => new { FieldId = id });
        services.AddScoped<SourceObjectService>();
        services.AddScoped<DatasetService>();
        services.AddScoped<DatasetVersionService>();
        services.AddScoped<DatasetVersionContentStore>();
        services.AddScoped<DatasetVersionCloner>();
        services.AddScoped<FieldService>();
        services.AddScoped<FieldUsage>();
        services.AddScoped<FieldsFromColumnsService>();
        services.AddScoped<MaterializedColumnService>();
        services.AddScoped<GrainService>();
        services.AddScoped<IPublicationRule, SourceObjectExistsRule>();
        services.AddScoped<IPublicationRule, VersionSupportsAModeRule>();
        services.AddScoped<IPublicationRule, DefaultTimeBindingInVersionRule>();
        services.AddScoped<IPublicationRule, FieldKeyUniqueRule>();
        services.AddScoped<IPublicationRule, FieldColumnsExistRule>();
        services.AddScoped<IPublicationRule, ExpressionFieldReferencesRule>();
        services.AddScoped<IPublicationRule, ExpressionFieldAcyclicRule>();
        services.AddScoped<IPublicationRule, GrainKeyUniqueRule>();
        services.AddScoped<IPublicationRule, GrainFieldsInVersionRule>();
        services.AddScoped<IPublicationRule, SeriesGrainExcludesTimeFieldRule>();
        return services;
    }
}
