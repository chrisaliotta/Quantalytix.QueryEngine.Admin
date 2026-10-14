namespace Quantalytix.QueryEngine.Admin.Api.Configuration;

/// <summary>Registration hook owned by the Datasets slice (datasets, dataset versions, fields and grains).</summary>
public static class DatasetsFeatureServiceCollectionExtensions
{
    /// <summary>Registers the Datasets slice's services, publication rules and lifecycle handlers.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDatasetsFeature(this IServiceCollection services)
    {
        return services;
    }
}
