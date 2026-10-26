using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Measures;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registration hook owned by the Bindings and Measures slice (dimension and time bindings, measures and their policies).</summary>
public static class BindingsAndMeasuresFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the binding and measure services, the binding namer other slices share, the formula dependency deriver, and the
    /// dataset version publication rules for bindings and measures, all scoped to the request.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddBindingsAndMeasuresFeature(this IServiceCollection services)
    {
        services.AddScoped<BindingService>();
        services.AddScoped<BindingNames>();
        services.AddScoped<MeasureService>();
        services.AddScoped<MeasureDependencyDeriver>();
        services.AddScoped<IPublicationRule, BindingFieldsInVersionRule>();
        services.AddScoped<IPublicationRule, BindingSourceFieldGroupableRule>();
        services.AddScoped<IPublicationRule, BoundRolesDistinctRule>();
        services.AddScoped<IPublicationRule, MappedBindingMappingSetDimensionRule>();
        services.AddScoped<IPublicationRule, RangeBindingDimensionKindRule>();
        services.AddScoped<IPublicationRule, TimeBindingDimensionKindRule>();
        services.AddScoped<IPublicationRule, BridgeBindingRouteRule>();
        services.AddScoped<IPublicationRule, UnknownMembersInVersionRule>();
        services.AddScoped<IPublicationRule, MeasureKeyUniqueRule>();
        services.AddScoped<IPublicationRule, ComponentFieldsInVersionRule>();
        services.AddScoped<IPublicationRule, ComponentRolesMatchKindRule>();
        services.AddScoped<IPublicationRule, FormulaReferencesKnownMeasuresRule>();
        services.AddScoped<IPublicationRule, FormulaAcyclicRule>();
        services.AddScoped<IPublicationRule, TimePolicySeriesGrainRule>();
        services.AddScoped<IPublicationRule, TimePolicyTimeBindingInVersionRule>();
        services.AddScoped<IPublicationRule, IntervalEstimatesForFinerPeriodsRule>();
        return services;
    }
}
