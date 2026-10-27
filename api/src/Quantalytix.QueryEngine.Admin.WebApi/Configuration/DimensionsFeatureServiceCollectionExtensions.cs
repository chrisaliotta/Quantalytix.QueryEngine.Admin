using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.WebApi.Routing;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registration hook owned by the Dimensions slice.</summary>
public static class DimensionsFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Dimensions slice, all scoped: its route resources (members and levels are found within the route's version),
    /// its reads and commands, the dimension change-set aggregate with its snapshot rules in the order they report, the same rules
    /// as the workflow's <c>dimension_version</c> publication rule, the freeze copier, the closure builder as a lifecycle handler,
    /// and the readers other slices use.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDimensionsFeature(this IServiceCollection services)
    {
        services.AddRouteResource<DimensionRecord>("dimensions", id => new { DimensionId = id });
        services.AddRouteResource<DimensionRoleRecord>("dimension-roles", id => new { DimensionRoleId = id });
        services.AddRouteResource<DimensionVersionRecord>("dimension-versions", id => new { DimensionVersionId = id });
        services.AddRouteResource<DimensionLevelRecord>("levels", (value, route) => new { DimensionLevelId = Id(value), DimensionVersionId = Id(route["versionId"]) });
        services.AddRouteResource<DimensionMemberRecord>("members", (value, route) => new { DimensionMemberId = Id(value), DimensionVersionId = Id(route["versionId"]) });

        services.AddScoped<DimensionQueries>();
        services.AddScoped<DimensionCommands>();
        services.AddScoped<DimensionChanges>();
        services.AddScoped<DimensionSnapshotLoader>();
        services.AddScoped<DimensionUsageReader>();
        services.AddScoped<DimensionConsumers>();
        services.AddScoped<DimensionDrafts>();
        services.AddScoped<DimensionVersionCloner>();
        services.AddScoped<DimensionVersionRemover>();
        services.AddScoped<ILivingVersionCopier, DimensionVersionCopier>();
        services.AddScoped<ReportDimensions>();
        services.AddScoped<PublishedDimensionReader>();
        services.AddScoped<ClosureBuilder>();
        services.AddScoped<IVersionLifecycleHandler>(provider => provider.GetRequiredService<ClosureBuilder>());

        services.AddChangeSetAggregate<DimensionAggregate, DimensionSnapshot, DimensionOperation, DimensionEditState>();
        services.AddSnapshotRule<DimensionSnapshot, RangeDataTypeMatchesKindRule>();
        services.AddSnapshotRule<DimensionSnapshot, LevelKeyUniqueRule>();
        services.AddSnapshotRule<DimensionSnapshot, MemberKeyUniqueRule>();
        services.AddSnapshotRule<DimensionSnapshot, MemberParentInVersionRule>();
        services.AddSnapshotRule<DimensionSnapshot, MemberLevelInVersionRule>();
        services.AddSnapshotRule<DimensionSnapshot, HierarchyAcyclicRule>();
        services.AddSnapshotRule<DimensionSnapshot, BandBoundaryFamilyRule>();
        services.AddSnapshotRule<DimensionSnapshot, BandBoundsOrderedRule>();
        services.AddSnapshotRule<DimensionSnapshot, BandsNonOverlappingRule>();
        services.AddSnapshotRule<DimensionSnapshot, BandOpenBoundsAtEndsRule>();
        services.AddSnapshotRule<DimensionSnapshot, BandMemberInVersionRule>();
        services.AddSnapshotRule<DimensionSnapshot, DimensionConsumersResolveRule>();
        services.AddSnapshotRule<DimensionSnapshot, ProfileRequiredKeysPresentRule>();
        services.AddSnapshotRule<DimensionSnapshot, ProfilePropertiesValidRule>();
        services.AddScoped<DimensionRules>();
        services.AddScoped<IPublicationRule, DimensionVersionRules>();
        return services;
    }

    /// <summary>A route id, which the route template constrains to digits.</summary>
    private static long Id(object? value)
    {
        return long.Parse((string)value!, CultureInfo.InvariantCulture);
    }
}
