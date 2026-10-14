using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Api.Configuration;

/// <summary>Registration hook owned by the Dimensions slice (dimensions, versions, levels, members, closure and range bands).</summary>
public static class DimensionsFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Dimensions slice's services, its <c>dimension_version</c> lifecycle handlers (the closure builder, then
    /// the predecessor closer) and its <c>dimension_version</c> publication rules, all scoped. The workflow runs handlers and
    /// rules in this order.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDimensionsFeature(this IServiceCollection services)
    {
        services.AddScoped<DimensionService>();
        services.AddScoped<DimensionVersionService>();
        services.AddScoped<DimensionLevelService>();
        services.AddScoped<DimensionMemberService>();
        services.AddScoped<RangeBandService>();
        services.AddScoped<DimensionVersionCloner>();
        services.AddScoped<DimensionVersionRemover>();
        services.AddScoped<DimensionDeleteChecks>();
        services.AddScoped<RangeBandSetReader>();

        services.AddScoped<IVersionLifecycleHandler, ClosureBuilder>();
        services.AddScoped<IVersionLifecycleHandler, PredecessorValidityCloser>();

        services.AddScoped<IPublicationRule, ValidFromRequiredRule>();
        services.AddScoped<IPublicationRule, ValidityOverlapRule>();
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
        return services;
    }
}
