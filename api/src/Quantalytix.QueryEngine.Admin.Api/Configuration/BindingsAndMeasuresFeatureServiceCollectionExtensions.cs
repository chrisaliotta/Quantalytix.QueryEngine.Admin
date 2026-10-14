namespace Quantalytix.QueryEngine.Admin.Api.Configuration;

/// <summary>Registration hook owned by the Bindings and Measures slice (dimension and time bindings, measures and their policies).</summary>
public static class BindingsAndMeasuresFeatureServiceCollectionExtensions
{
    /// <summary>Registers the Bindings and Measures slice's services, publication rules and lifecycle handlers.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddBindingsAndMeasuresFeature(this IServiceCollection services)
    {
        return services;
    }
}
