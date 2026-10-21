using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.ReportEditor;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Configuration;

/// <summary>Registration hook owned by the Calculation models and Reports slice (models, model versions and their metrics, reports, their editor and their content).</summary>
public static class CalculationModelsAndReportsFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the slice's services, its <c>calculation_model_version</c> publication rules and the report rules, all scoped.
    /// Each content kind is registered under <see cref="IOwnedContent{TItem, TInput}"/>, a report's also under
    /// <see cref="IReportContent{TItem, TInput}"/>, with the validator of its item routes. The report rules run when a report is
    /// enabled and on every write to an enabled report; the ones that read what a model version or a release gives reports also run,
    /// over the enabled reports, when such a version is validated, after the release slice's own rules.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCalculationModelsAndReportsFeature(this IServiceCollection services)
    {
        services.AddScoped<ContentKeys>();

        services.AddScoped<CalculationModelService>();
        services.AddScoped<CalculationModelVersionService>();
        services.AddScoped<ModelVersionCloner>();
        services.AddScoped<ModelKeys>();
        services.AddScoped<ReportUses>();
        services.AddModelContent<VariableService, VariableRecord, VariableInput>();
        services.AddModelContent<CalculatedMeasureService, CalculatedMeasureRecord, CalculatedMeasureInput>();

        services.AddScoped<IPublicationRule, FormulaReferencesResolveRule>();
        services.AddScoped<IPublicationRule, FormulasAcyclicRule>();

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
        services.AddConsumerRule<ReportMetricsResolveRule>(VersionKinds.CalculationModelVersion);
        services.AddConsumerRule<ProfilesCompleteRule>(VersionKinds.CalculationModelVersion);
        services.AddConsumerRule<ReportSourcesResolveRule>(VersionKinds.SemanticRelease);
        return services;
    }

    /// <summary>Registers a report rule as a publication rule of a kind whose versions change what enabled reports read.</summary>
    private static void AddConsumerRule<TRule>(this IServiceCollection services, string kind)
        where TRule : IReportRule, new()
    {
        services.AddScoped<IPublicationRule>(provider => new ReportConsumerRule(kind, new TRule(), provider.GetRequiredService<ReportGraphReader>()));
    }

    /// <summary>Registers one model version content kind's service and the validator that checks its item routes.</summary>
    private static void AddModelContent<TService, TItem, TInput>(this IServiceCollection services)
        where TService : class, IOwnedContent<TItem, TInput>
    {
        services.AddScoped<IOwnedContent<TItem, TInput>, TService>();
        services.AddScoped<IValidator<VersionItemReference<TItem>>, OwnedItemReferenceValidator<VersionItemReference<TItem>, TItem, TInput>>();
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
