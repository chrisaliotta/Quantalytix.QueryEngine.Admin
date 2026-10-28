using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.WebApi.Routing;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registration hook owned by the Mappings and Bridges slice (mapping sets, mapping versions, bridges and bridge versions).</summary>
public static class MappingsAndBridgesFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Mappings and Bridges slice, all scoped: its route resources, the readers both kinds share, and for each kind its
    /// reads and commands, its change-set aggregate with its snapshot rules in the order they report, the same rules as the workflow's
    /// publication rule of the kind, and the freeze copier; plus <see cref="ReportBridges"/>, the reports module's way to write report
    /// bridges.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMappingsAndBridgesFeature(this IServiceCollection services)
    {
        services.AddRouteResource<MappingSetRecord>("mapping-sets", id => new { MappingSetId = id });
        services.AddRouteResource<MappingVersionRecord>("mapping-versions", id => new { MappingVersionId = id });
        services.AddRouteResource<BridgeRecord>("bridges", id => new { BridgeId = id });
        services.AddRouteResource<BridgeVersionRecord>("bridge-versions", id => new { BridgeVersionId = id });

        services.AddScoped<MappingReferenceReader>();
        services.AddScoped(typeof(SnapshotRuleRunner<>));

        services.AddScoped<MappingQueries>();
        services.AddScoped<MappingCommands>();
        services.AddScoped<MappingSetChanges>();
        services.AddScoped<MappingSetSnapshotLoader>();
        services.AddScoped<MappingDrafts>();
        services.AddScoped<MappingVersionRemover>();
        services.AddScoped<ILivingVersionCopier, MappingVersionCopier>();
        services.AddChangeSetAggregate<MappingSetAggregate, MappingSetSnapshot, MappingOperation, MappingSetEditState>();
        services.AddSnapshotRule<MappingSetSnapshot, DimensionVersionPublishedRule>();
        services.AddSnapshotRule<MappingSetSnapshot, SourceKeyUniqueRule>();
        services.AddSnapshotRule<MappingSetSnapshot, EntryMemberInVersionRule>();
        services.AddSnapshotRule<MappingSetSnapshot, EntryMemberAssignableRule>();
        services.AddScoped<IPublicationRule, MappingVersionRules>();

        services.AddScoped<BridgeQueries>();
        services.AddScoped<BridgeCommands>();
        services.AddScoped<BridgeChanges>();
        services.AddScoped<ReportBridges>();
        services.AddScoped<BridgeSnapshotLoader>();
        services.AddScoped<BridgeOwnerReader>();
        services.AddScoped<BridgeDrafts>();
        services.AddScoped<BridgeStore>();
        services.AddScoped<ILivingVersionCopier, BridgeVersionCopier>();
        services.AddChangeSetAggregate<BridgeAggregate, BridgeSnapshot, BridgeOperation, BridgeEditState>();
        services.AddSnapshotRule<BridgeSnapshot, EndpointVersionsPublishedRule>();
        services.AddSnapshotRule<BridgeSnapshot, MappingMembersInEndpointVersionsRule>();
        services.AddSnapshotRule<BridgeSnapshot, PairUniqueRule>();
        services.AddSnapshotRule<BridgeSnapshot, CardinalityManyToOneRule>();
        services.AddSnapshotRule<BridgeSnapshot, AllocationNoneRule>();
        services.AddSnapshotRule<BridgeSnapshot, AllocationPartitionRule>();
        services.AddSnapshotRule<BridgeSnapshot, AllocationReplicateRule>();
        services.AddSnapshotRule<BridgeSnapshot, DescendantsReportOnlyRule>();
        services.AddScoped<IPublicationRule, BridgeVersionRules>();
        return services;
    }
}
