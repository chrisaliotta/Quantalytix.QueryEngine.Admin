using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.WebApi.Routing;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registration hook owned by the Dimensions slice (dimensions, roles, versions, levels, members, closure and range bands).</summary>
public static class DimensionsFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Dimensions slice's services, its <c>dimension_version</c> lifecycle handler (the closure builder, which member
    /// writes also call) and its <c>dimension_version</c> publication rules, all scoped. The workflow runs rules in this order.
    /// Also registers the slice's route resources, which the shared 404 check reads.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDimensionsFeature(this IServiceCollection services)
    {
        services.AddRouteResource<DimensionRecord>("dimensions", id => new { DimensionId = id });
        services.AddRouteResource<DimensionRoleRecord>("dimension-roles", id => new { DimensionRoleId = id });
        services.AddRouteResource<DimensionRoleRecord>("roles", id => new { DimensionRoleId = id });
        services.AddRouteResource<DimensionVersionRecord>("dimension-versions", id => new { DimensionVersionId = id });
        services.AddRouteResource<DimensionLevelRecord>("levels", id => new { DimensionLevelId = id });
        services.AddRouteResource<DimensionMemberRecord>("members", id => new { DimensionMemberId = id });
        services.AddScoped<DimensionService>();
        services.AddScoped<DimensionRoleService>();
        services.AddScoped<DimensionVersionService>();
        services.AddScoped<DimensionLevelService>();
        services.AddScoped<DimensionMemberService>();
        services.AddScoped<RangeBandService>();
        services.AddScoped<DimensionVersionCloner>();
        services.AddScoped<ILivingVersionCopier, DimensionVersionCopier>();
        services.AddScoped<DimensionVersionRemover>();
        services.AddScoped<DimensionDeleteChecks>();
        services.AddScoped<DimensionConsumers>();
        services.AddScoped<PublishedDimensionReader>();
        services.AddScoped<RangeBandSetReader>();

        services.AddScoped<ClosureBuilder>();
        services.AddScoped<IVersionLifecycleHandler>(provider => provider.GetRequiredService<ClosureBuilder>());

        services.AddScoped<IPublicationRule, RangeDataTypeMatchesKindRule>();
        services.AddScoped<IPublicationRule, LevelKeyUniqueRule>();
        services.AddScoped<IPublicationRule, MemberKeyUniqueRule>();
        services.AddScoped<IPublicationRule, MemberParentInVersionRule>();
        services.AddScoped<IPublicationRule, MemberLevelInVersionRule>();
        services.AddScoped<IPublicationRule, HierarchyAcyclicRule>();
        services.AddScoped<IPublicationRule, BandBoundaryFamilyRule>();
        services.AddScoped<IPublicationRule, BandBoundsOrderedRule>();
        services.AddScoped<IPublicationRule, BandsNonOverlappingRule>();
        services.AddScoped<IPublicationRule, BandMemberInVersionRule>();
        services.AddScoped<IPublicationRule, DimensionConsumersResolveRule>();
        return services;
    }
}
