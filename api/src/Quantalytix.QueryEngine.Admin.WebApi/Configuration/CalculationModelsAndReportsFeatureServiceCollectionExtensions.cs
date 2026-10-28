using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.ReportEditor;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.WebApi.Routing;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registration hook owned by the Calculation models and Reports slice (models and their metrics, reports, their editor and their content).</summary>
public static class CalculationModelsAndReportsFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Calculation models slice through <see cref="CalculationModelsFeatureServiceCollectionExtensions"/>, then the
    /// report services and the report rules, all scoped. Each report content kind is registered under
    /// <see cref="IOwnedContent{TItem, TInput}"/> and <see cref="IReportContent{TItem, TInput}"/>, with the validator of its item
    /// routes. The report rules run when a report is enabled and on every write to an enabled report; the model gate runs the ones
    /// that read a model's metrics and parameters on every model change set; and the report rules that read what a release gives
    /// reports also run, over the enabled reports, when a release is validated, after the release slice's own rules.
    /// Also registers the slice's route resources, which the shared 404 check reads.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCalculationModelsAndReportsFeature(this IServiceCollection services)
    {
        services.AddCalculationModelsFeature();
        services.AddScoped<ContentKeys>();

        services.AddRouteResource<ReportRecord>("reports", id => new { ReportId = id });

        services.AddScoped<ReportService>();
        services.AddScoped<ReportStatusService>();
        services.AddScoped<ReportOwnership>();
        services.AddScoped<ReportNames>();
        services.AddScoped<OwnedVersions>();
        services.AddScoped<ReleaseDatasets>();
        services.AddScoped<ReportContentRemover>();
        services.AddScoped<ReportColumnService>();
        services.AddScoped<ReportVariableScopeService>();
        services.AddScoped<ReportScenarioService>();
        services.AddScoped<ReportStyleService>();
        services.AddScoped<ModelMetricsReader>();
        services.AddScoped<FormulaRowReferences>();
        services.AddScoped<ReportGraphReader>();
        services.AddScoped<ReportRules>();
        services.AddScoped<ReportChanges>();
        services.AddReportContent<SourceProfileService, SourceProfile, SourceProfileInput>();
        services.AddReportContent<CalculationDimensionService, CalculationDimensionRecord, CalculationDimensionInput>();

        services.AddScoped<ReportEditorReader>();
        services.AddScoped<ReportRowService>();
        services.AddScoped<ReportSourceService>();
        services.AddScoped<ReportSourceVariableService>();
        services.AddScoped<SourceItemService>();
        services.AddScoped<SourceCoverageReader>();

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
        services.AddConsumerRule<ReportSourcesResolveRule>();
        services.AddConsumerRule<ReportFactorsApplyRule>();
        return services;
    }

    /// <summary>Registers a report rule as a publication rule of releases, whose dataset versions change what enabled reports read.</summary>
    private static void AddConsumerRule<TRule>(this IServiceCollection services)
        where TRule : IReportRule, new()
    {
        services.AddScoped<IPublicationRule>(provider => new ReportConsumerRule(new TRule(), provider.GetRequiredService<ReportGraphReader>()));
    }

    /// <summary>Registers one report content kind's service under both its interfaces and the validator that checks its item routes.</summary>
    private static void AddReportContent<TService, TItem, TInput>(this IServiceCollection services)
        where TService : class, IReportContent<TItem, TInput>
    {
        services.AddScoped<TService>();
        services.AddScoped<IReportContent<TItem, TInput>>(provider => provider.GetRequiredService<TService>());
        services.AddScoped<IOwnedContent<TItem, TInput>>(provider => provider.GetRequiredService<TService>());
        services.AddScoped<IValidator<ReportItemReference<TItem>>, OwnedItemReferenceValidator<ReportItemReference<TItem>, TItem, TInput>>();
    }
}
