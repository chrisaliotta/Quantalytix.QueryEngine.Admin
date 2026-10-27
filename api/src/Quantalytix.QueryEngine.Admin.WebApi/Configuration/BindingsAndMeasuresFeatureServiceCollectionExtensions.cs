using Quantalytix.QueryEngine.Admin.Application.Bindings;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>
/// Registration hook owned by the Bindings and Measures slice. Their edits and rules belong to the dataset version aggregate, which
/// <see cref="DatasetsFeatureServiceCollectionExtensions"/> registers in rule order; this hook registers what other slices share.
/// </summary>
public static class BindingsAndMeasuresFeatureServiceCollectionExtensions
{
    /// <summary>Registers the binding namer the dimension, mapping, bridge and release slices share, scoped to the request.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddBindingsAndMeasuresFeature(this IServiceCollection services)
    {
        services.AddScoped<BindingNames>();
        return services;
    }
}
