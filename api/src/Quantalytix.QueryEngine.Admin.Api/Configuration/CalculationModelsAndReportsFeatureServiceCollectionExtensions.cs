using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.ReportEditor;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Api.Configuration;

/// <summary>Registration hook owned by the Calculation models and Reports slice (models and their metrics, reports, their editor and their content).</summary>
public static class CalculationModelsAndReportsFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the slice's services and the report rules, all scoped. Each content kind is registered under
    /// <see cref="IOwnedContent{TItem, TInput}"/>, a model's also under <see cref="IModelContent{TItem, TInput}"/> and a report's
    /// under <see cref="IReportContent{TItem, TInput}"/>, with the validator of its item routes. The report rules run when a report
    /// is enabled and on every write to an enabled report; <see cref="ModelChanges"/> runs the model's formula rules and the report
    /// rules that read its metrics on every write to a model's metrics; and the report rules that read what a release gives reports
    /// also run, over the enabled reports, when a release is validated, after the release slice's own rules.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCalculationModelsAndReportsFeature(this IServiceCollection services)
    {
        services.AddScoped<ContentKeys>();

        services.AddScoped<CalculationModelService>();
        services.AddScoped<ModelKeys>();
        services.AddScoped<ReportUses>();
        services.AddScoped<ModelChanges>();
        services.AddModelContent<VariableService, VariableRecord, VariableInput>();
        services.AddModelContent<CalculatedMeasureService, CalculatedMeasureRecord, CalculatedMeasureInput>();

        services.AddScoped<ReportService>();
        services.AddScoped<ReportStatusService>();
        services.AddScoped<ReportOwnership>();
        services.AddScoped<ReportNames>();
        services.AddScoped<OwnedVersions>();
        services.AddScoped<ReleaseDatasets>();
        services.AddScoped<ReportContentRemover>();
        services.AddScoped<ReportColumnService>();
        services.AddScoped<ReportVariableScopeService>();
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
        services.AddScoped<IReportRule, DefaultProfileEnabledRule>();
        services.AddScoped<IReportRule, ReportDefaultsResolveRule>();
        services.AddScoped<IReportRule, ColumnKeysDistinctRule>();
        services.AddScoped<IReportRule, ReportMetricsResolveRule>();
        services.AddScoped<IReportRule, ProfilesCompleteRule>();
        services.AddScoped<IReportRule, ReportSourcesResolveRule>();
        services.AddScoped<IReportRule, ReportFactorsApplyRule>();
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

    /// <summary>Registers one model content kind's service under both its interfaces and the validator that checks its item routes.</summary>
    private static void AddModelContent<TService, TItem, TInput>(this IServiceCollection services)
        where TService : class, IModelContent<TItem, TInput>
    {
        services.AddScoped<TService>();
        services.AddScoped<IModelContent<TItem, TInput>>(provider => provider.GetRequiredService<TService>());
        services.AddScoped<IOwnedContent<TItem, TInput>>(provider => provider.GetRequiredService<TService>());
        services.AddScoped<IValidator<ModelItemReference<TItem>>, OwnedItemReferenceValidator<ModelItemReference<TItem>, TItem, TInput>>();
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
