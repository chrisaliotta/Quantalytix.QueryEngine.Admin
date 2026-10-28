using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.WebApi.Routing;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registration hook owned by the Calculation models and Reports slices.</summary>
public static class CalculationModelsAndReportsFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Calculation models slice through <see cref="CalculationModelsFeatureServiceCollectionExtensions"/>, then the
    /// reports slice, all scoped: its route resource, its reads and commands, the report change-set aggregate with the report gate,
    /// which refuses a change that leaves an enabled report breaking a rule, and the findings of a disabled one, and the report rules
    /// in reporting order, which enabling, the gate, the model gate and the release checks run.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCalculationModelsAndReportsFeature(this IServiceCollection services)
    {
        services.AddCalculationModelsFeature();
        services.AddRouteResource<ReportRecord>("reports", id => new { ReportId = id });

        services.AddScoped<ReportQueries>();
        services.AddScoped<ReportCommands>();
        services.AddScoped<ReportRemover>();
        services.AddScoped<ReportChanges>();
        services.AddScoped<ReportDiagnosticsBuilder>();
        services.AddScoped<ReportSnapshotLoader>();
        services.AddScoped<ReportGraphReader>();
        services.AddScoped<ReportRules>();
        services.AddScoped<OwnedVersions>();
        services.AddScoped<ReleaseDatasets>();
        services.AddScoped<ReportSourceWrites>();

        services.AddChangeSetAggregate<ReportAggregate, ReportSnapshot, ReportOperation, ReportDocument>();
        services.AddSnapshotRule<ReportSnapshot, ReportGateRule>();
        services.AddSnapshotRule<ReportSnapshot, ReportFindingsRule>();

        services.AddScoped<IReportRule, ReportItemsResolveRule>();
        services.AddScoped<IReportRule, CalculationGroupingResolveRule>();
        services.AddScoped<IReportRule, ScenarioRolesDistinctRule>();
        services.AddScoped<IReportRule, DefaultProfileEnabledRule>();
        services.AddScoped<IReportRule, ReportDefaultsResolveRule>();
        services.AddScoped<IReportRule, ColumnKeysDistinctRule>();
        services.AddScoped<IReportRule, ReportMetricsResolveRule>();
        services.AddScoped<IReportRule, ProfilesCompleteRule>();
        services.AddScoped<IReportRule, ReportSourcesResolveRule>();
        services.AddScoped<IReportRule, ReportFactorsApplyRule>();
        services.AddScoped<IReportRule, ReportRowParametersResolveRule>();
        services.AddScoped<IReportRule, ReportStylesResolveRule>();
        return services;
    }
}
