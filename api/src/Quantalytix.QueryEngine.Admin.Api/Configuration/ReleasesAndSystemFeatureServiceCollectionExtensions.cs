namespace Quantalytix.QueryEngine.Admin.Api.Configuration;

/// <summary>Registration hook owned by the Releases and System slice (semantic releases, assumption sets, tenant directory and database info).</summary>
public static class ReleasesAndSystemFeatureServiceCollectionExtensions
{
    /// <summary>Registers the Releases and System slice's services, publication rules and lifecycle handlers.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddReleasesAndSystemFeature(this IServiceCollection services)
    {
        return services;
    }
}
