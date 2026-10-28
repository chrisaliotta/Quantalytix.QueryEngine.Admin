using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.WebApi.Routing;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registration hook owned by the Releases slice (semantic releases and assumption sets).</summary>
public static class ReleasesAndSystemFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Releases slice's services, publication rules, retirement rule and lifecycle handlers, all scoped: releases
    /// with their activation, and assumption sets with a pinner for each living kind that repoints the active set as a version
    /// is published. Also registers the slice's route resources, which the shared 404 check reads.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddReleasesAndSystemFeature(this IServiceCollection services)
    {
        services.AddRouteResource<SemanticReleaseRecord>("releases", id => new { SemanticReleaseId = id });
        services.AddRouteResource<AssumptionSetRecord>("assumption-sets", id => new { AssumptionSetId = id });
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

        return services;
    }
}
