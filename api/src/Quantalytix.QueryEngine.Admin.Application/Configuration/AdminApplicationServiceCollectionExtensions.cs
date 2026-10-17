using Microsoft.Extensions.DependencyInjection;
using Quantalytix.QueryEngine.Admin.Application.Lookups;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Configuration;

/// <summary>Registers the application services every feature slice builds on.</summary>
public static class AdminApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers tenant listing, lookups, the eight version stores, their registry, the version workflow and the semantic
    /// revision, all scoped to the request. The API registers the FluentValidation validators by scanning this assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAdminApplication(this IServiceCollection services)
    {
        services.AddScoped<AccessibleTenantsService>();
        services.AddScoped<LookupService>();
        services.AddScoped<CalculationLookups>();
        services.AddScoped<IVersionStore, DatasetVersionStore>();
        services.AddScoped<IVersionStore, DimensionVersionStore>();
        services.AddScoped<IVersionStore, MappingVersionStore>();
        services.AddScoped<IVersionStore, BridgeVersionStore>();
        services.AddScoped<IVersionStore, SemanticReleaseStore>();
        services.AddScoped<IVersionStore, AssumptionSetStore>();
        services.AddScoped<IVersionStore, CalculationModelVersionStore>();
        services.AddScoped<IVersionStore, ReportVersionStore>();
        services.AddScoped<VersionStoreRegistry>();
        services.AddScoped<VersionWorkflow>();
        services.AddScoped<SemanticRevision>();
        return services;
    }
}
