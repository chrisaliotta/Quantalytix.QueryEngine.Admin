using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Configuration;

/// <summary>Registration hook owned by the Calculation models and Reports slice (models, model versions and their content, reports and report versions).</summary>
public static class CalculationModelsAndReportsFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the slice's services and its <c>calculation_model_version</c> and <c>report_version</c> publication rules, all
    /// scoped. Each model content kind is registered under <see cref="IModelVersionContent{TItem, TInput}"/> with the validator of
    /// its item routes. The report rules run for report versions and, against the version being published, for the published
    /// reports of a calculation model version; the rules that check reports against a release run for semantic releases too,
    /// after the release slice's own rules. The workflow runs the rules of a kind in this order.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCalculationModelsAndReportsFeature(this IServiceCollection services)
    {
        services.AddScoped<CalculationModelService>();
        services.AddScoped<CalculationModelVersionService>();
        services.AddScoped<ModelVersionCloner>();
        services.AddScoped<ModelContentRemover>();
        services.AddScoped<ModelKeys>();
        services.AddScoped<ReportUses>();
        services.AddModelContent<VariableService, VariableRecord, VariableInput>();
        services.AddModelContent<VariableSourceService, VariableSourceRecord, VariableSourceInput>();
        services.AddModelContent<SourceProfileService, SourceProfile, SourceProfileInput>();
        services.AddModelContent<CalculatedMeasureService, CalculatedMeasureRecord, CalculatedMeasureInput>();
        services.AddModelContent<CalculationDimensionService, CalculationDimensionRecord, CalculationDimensionInput>();
        services.AddModelContent<CalculationRouteService, CalculationRouteRecord, CalculationRouteInput>();

        services.AddScoped<IPublicationRule, FormulaReferencesResolveRule>();
        services.AddScoped<IPublicationRule, FormulasAcyclicRule>();
        services.AddScoped<IPublicationRule, ProfilesCompleteRule>();

        services.AddScoped<ReportService>();
        services.AddScoped<ReportVersionService>();
        services.AddScoped<ReportVersionCloner>();
        services.AddScoped<ReportColumnService>();
        services.AddScoped<ReportLineService>();
        services.AddScoped<ModelMetricsReader>();
        services.AddScoped<ReportGraphReader>();

        services.AddScoped<IPublicationRule, ModelVersionPublishedRule>();
        foreach (var kind in new[] { VersionKinds.ReportVersion, VersionKinds.CalculationModelVersion })
        {
            services.AddRuleFor<DefaultProfileEnabledRule>(kind);
            services.AddRuleFor<ReportDefaultsResolveRule>(kind);
            services.AddRuleFor<ColumnKeysDistinctRule>(kind);
            services.AddRuleFor<ReportMetricsResolveRule>(kind);
            services.AddRuleFor<ReportItemsResolveRule>(kind);
        }
        foreach (var kind in new[] { VersionKinds.ReportVersion, VersionKinds.CalculationModelVersion, VersionKinds.SemanticRelease })
        {
            services.AddRuleFor<ReportSourcesResolveRule>(kind);
            services.AddRuleFor<CalculationRoutesResolveRule>(kind);
        }
        return services;
    }

    /// <summary>Registers a report rule for one kind; <see cref="ReportGraphReader"/> gives each kind its scope of reports.</summary>
    private static void AddRuleFor<TRule>(this IServiceCollection services, string kind)
        where TRule : IPublicationRule
    {
        services.AddScoped<IPublicationRule>(provider => ActivatorUtilities.CreateInstance<TRule>(provider, kind));
    }

    /// <summary>Registers one model content kind's service and the validator that checks its item routes.</summary>
    private static void AddModelContent<TService, TItem, TInput>(this IServiceCollection services)
        where TService : class, IModelVersionContent<TItem, TInput>
    {
        services.AddScoped<IModelVersionContent<TItem, TInput>, TService>();
        services.AddScoped<IValidator<ModelItemReference<TItem>>, ModelItemReferenceValidator<TItem, TInput>>();
    }
}
