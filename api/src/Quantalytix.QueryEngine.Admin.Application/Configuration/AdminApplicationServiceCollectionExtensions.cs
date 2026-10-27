using Microsoft.Extensions.DependencyInjection;
using Quantalytix.QueryEngine.Admin.Application.Lookups;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Configuration;

/// <summary>Registers the application services every feature slice builds on.</summary>
public static class AdminApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers tenant listing, lookups, the five version stores, their registry, the version workflow with the publication set
    /// its rules read, a predecessor retirer for each living kind, the semantic revision and the living kinds' freeze, all scoped
    /// to the request. The API registers the FluentValidation validators by scanning this assembly.
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
        services.AddScoped<VersionStoreRegistry>();
        services.AddScoped<PublicationSet>();
        services.AddScoped<VersionWorkflow>();
        foreach (var kind in VersionKinds.Living)
            services.AddScoped<IVersionLifecycleHandler>(provider => new PredecessorRetirer(kind, provider.GetRequiredService<VersionStoreRegistry>(), provider.GetRequiredService<TimeProvider>()));
        services.AddScoped<SemanticRevision>();
        services.AddScoped<LivingVersionFreezer>();
        return services;
    }
}
