namespace Quantalytix.QueryEngine.Admin.Api.Configuration;

/// <summary>Registration hook owned by the Dimensions slice (dimensions, versions, levels, members, closure and range bands).</summary>
public static class DimensionsFeatureServiceCollectionExtensions
{
    /// <summary>Registers the Dimensions slice's services, publication rules and lifecycle handlers.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDimensionsFeature(this IServiceCollection services)
    {
        return services;
    }
}
