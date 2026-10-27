using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.WebApi.Routing;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registration hook owned by the Mappings and Bridges slice (mapping sets, mapping versions, bridges and bridge versions).</summary>
public static class MappingsAndBridgesFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Mappings and Bridges slice's services and its <c>mapping_version</c> and <c>bridge_version</c> publication
    /// rules, all scoped. The workflow runs the rules of a kind in this order.
    /// Also registers the slice's route resources, which the shared 404 check reads.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMappingsAndBridgesFeature(this IServiceCollection services)
    {
        services.AddRouteResource<MappingSetRecord>("mapping-sets", id => new { MappingSetId = id });
        services.AddRouteResource<MappingVersionRecord>("mapping-versions", id => new { MappingVersionId = id });
        services.AddRouteResource<BridgeRecord>("bridges", id => new { BridgeId = id });
        services.AddRouteResource<BridgeVersionRecord>("bridge-versions", id => new { BridgeVersionId = id });
        services.AddScoped<MappingSetService>();
        services.AddScoped<MappingVersionService>();
        services.AddScoped<MappingEntryService>();
        services.AddScoped<MappingVersionCloner>();
        services.AddScoped<MappingEntrySetReader>();

        services.AddScoped<IPublicationRule, DimensionVersionPublishedRule>();
        services.AddScoped<IPublicationRule, SourceKeyUniqueRule>();
        services.AddScoped<IPublicationRule, EntryMemberInVersionRule>();
        services.AddScoped<IPublicationRule, EntryMemberAssignableRule>();

        services.AddScoped<BridgeService>();
        services.AddScoped<ILivingVersionCopier, MappingVersionCopier>();
        services.AddScoped<ILivingVersionCopier, BridgeVersionCopier>();
        services.AddScoped<BridgeVersionService>();
        services.AddScoped<BridgeMappingService>();
        services.AddScoped<BridgeVersionCloner>();
        services.AddScoped<BridgeMappingSetReader>();

        services.AddScoped<IPublicationRule, EndpointVersionsPublishedRule>();
        services.AddScoped<IPublicationRule, MappingMembersInEndpointVersionsRule>();
        services.AddScoped<IPublicationRule, CardinalityManyToOneRule>();
        services.AddScoped<IPublicationRule, AllocationNoneRule>();
        services.AddScoped<IPublicationRule, AllocationPartitionRule>();
        services.AddScoped<IPublicationRule, AllocationReplicateRule>();
        return services;
    }
}
