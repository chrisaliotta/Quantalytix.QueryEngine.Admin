using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Changes;
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
    /// Registers the Releases slice, all scoped: its route resources; the release reads, commands and manifest change-set aggregate
    /// with the release rules in the contract's order (<c>ReleaseRuleCode</c>), the gate the pipeline runs, and the same rules as the
    /// workflow's <c>semantic_release</c> publication rule; the retirement rule; and the assumption set reads and commands with a
    /// pinner for each living kind that repoints the active set as a version is published.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddReleasesAndSystemFeature(this IServiceCollection services)
    {
        services.AddRouteResource<SemanticReleaseRecord>("releases", id => new { SemanticReleaseId = id });
        services.AddRouteResource<AssumptionSetRecord>("assumption-sets", id => new { AssumptionSetId = id });

        services.AddScoped<ReleaseSnapshotLoader>();
        services.AddScoped<ReleaseManifestReader>();
        services.AddScoped<ReleaseReportReader>();
        services.AddScoped<ManifestDatasetReader>();
        services.AddScoped<ReleaseReportChecks>();
        services.AddScoped<ReleaseRules>();
        services.AddScoped<ReleaseQueries>();
        services.AddScoped<ReleaseCommands>();
        services.AddScoped<ReleaseChanges>();
        services.AddChangeSetAggregate<ReleaseAggregate, ReleaseSnapshot, ReleaseOperation, ReleaseDetail>();
        services.AddSnapshotRule<ReleaseSnapshot, ReleaseGateRule>();
        services.AddScoped<IReleaseRule, ManifestNotEmptyRule>();
        services.AddScoped<IReleaseRule, PinnedVersionBelongsToDatasetRule>();
        services.AddScoped<IReleaseRule, PinnedDatasetVersionPublishedRule>();
        services.AddScoped<IReleaseRule, BoundDimensionsResolvableRule>();
        services.AddScoped<IReleaseRule, MappedBindingsResolvableRule>();
        services.AddScoped<IReleaseRule, BridgeBindingsResolvableRule>();
        services.AddScoped<IPublicationRule, ReleaseVersionRules>();
        services.AddScoped<ReleaseNotActiveRule>();
        services.AddScoped<IRetirementRule>(provider => provider.GetRequiredService<ReleaseNotActiveRule>());

        services.AddScoped<AssumptionPins>();
        services.AddScoped<PinIdentityReader>();
        services.AddScoped<AssumptionSetQueries>();
        services.AddScoped<AssumptionSetCommands>();
        foreach (var kind in VersionKinds.Living)
        {
            services.AddScoped<IVersionLifecycleHandler>(provider =>
                new ActiveSetPinner(kind, provider.GetRequiredService<VersionStoreRegistry>(), provider.GetRequiredService<AssumptionPins>()));
        }

        return services;
    }
}
