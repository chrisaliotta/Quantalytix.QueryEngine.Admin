using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Overview;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.SystemTenants;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.TenantVerification;

namespace Quantalytix.QueryEngine.Admin.Api.Configuration;

/// <summary>Registration hook owned by the Releases and System slice (semantic releases, assumption sets, tenant directory and database info).</summary>
public static class ReleasesAndSystemFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Releases and System slice's services, publication rules and retirement rule, all scoped: releases and
    /// their activation, assumption sets, the tenant overview, and the tenant directory with its connection verification and
    /// masking. The slice has no lifecycle handlers.
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
        services.AddScoped<AssumptionPinReader>();
        services.AddScoped<IPublicationRule, PinsNotEmptyRule>();
        services.AddScoped<IPublicationRule, PinnedVersionBelongsToDimensionRule>();
        services.AddScoped<IPublicationRule, PinnedDimensionVersionPublishedRule>();

        services.AddScoped<OverviewService>();

        services.AddScoped<SystemTenantService>();
        services.AddScoped<TenantConnectionVerification>();
        services.AddScoped<ITenantDatabaseVerifier, TenantDatabaseVerifier>();
        services.AddScoped<IConnectionStringMasker, ConnectionStringMasker>();
        return services;
    }
}
