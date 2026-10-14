namespace Quantalytix.QueryEngine.Admin.Api.Configuration;

/// <summary>Registration hook owned by the Mappings and Bridges slice (mapping sets, mapping versions, bridges and bridge versions).</summary>
public static class MappingsAndBridgesFeatureServiceCollectionExtensions
{
    /// <summary>Registers the Mappings and Bridges slice's services, publication rules and lifecycle handlers.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMappingsAndBridgesFeature(this IServiceCollection services)
    {
        return services;
    }
}
