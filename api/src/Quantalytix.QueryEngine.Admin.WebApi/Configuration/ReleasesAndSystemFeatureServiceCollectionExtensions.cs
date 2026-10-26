using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Overview;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.SystemTenants;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.TenantVerification;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registration hook owned by the Releases and System slice (semantic releases, assumption sets, tenant directory and database info).</summary>
public static class ReleasesAndSystemFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Releases and System slice's services, publication rules, retirement rule and lifecycle handlers, all scoped:
    /// releases with their activation, assumption sets with a pinner for each living kind that repoints the active set as a
    /// version is published, the tenant overview, and the tenant directory with its connection verification and masking.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddReleasesAndSystemFeature(this IServiceCollection services)
    {
        services.AddScoped<ReleaseService>();
        services.AddScoped<ReleaseActivator>();
        services.AddScoped<ReleaseManifestReader>();
        services.AddScoped<IPublicationRule, ManifestNotEmptyRule>();
        services.AddScoped<IPublicationRule, PinnedVersionBelongsToDatasetRule>();
        services.AddScoped<IPublicationRule, PinnedDatasetVersionPublishedRule>();
        services.AddScoped<IPublicationRule, BoundDimensionsResolvableRule>();
        services.AddScoped<IPublicationRule, MappedBindingsResolvableRule>();
        services.AddScoped<IPublicationRule, BridgeBindingsResolvableRule>();
        services.AddScoped<IRetirementRule, ReleaseNotActiveRule>();

        services.AddScoped<AssumptionSetService>();
        services.AddScoped<AssumptionPins>();
        services.AddScoped<AssumptionPinReader>();
        foreach (var kind in VersionKinds.Living)
        {
            services.AddScoped<IVersionLifecycleHandler>(provider =>
                new ActiveSetPinner(kind, provider.GetRequiredService<VersionStoreRegistry>(), provider.GetRequiredService<AssumptionPins>()));
        }

        services.AddScoped<OverviewService>();

        services.AddScoped<SystemTenantService>();
        services.AddScoped<TenantConnectionVerification>();
        services.AddScoped<ITenantDatabaseVerifier, TenantDatabaseVerifier>();
        services.AddScoped<IConnectionStringMasker, ConnectionStringMasker>();
        return services;
    }
}
