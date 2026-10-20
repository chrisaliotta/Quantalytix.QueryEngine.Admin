using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Configuration;

/// <summary>Registration hook owned by the Calculation models and Reports slice (models, model versions and their metrics, reports and report versions and their content).</summary>
public static class CalculationModelsAndReportsFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the slice's services and its <c>calculation_model_version</c> and <c>report_version</c> publication rules, all
    /// scoped. Each version content kind is registered under <see cref="IVersionContent{TItem, TInput}"/> with the validator of its
    /// item routes. The rules that check a report's metrics run for report versions and, against the version being published, for
    /// the published reports of a calculation model version; the rules that check reports against a release run for semantic
    /// releases too, after the release slice's own rules. The workflow runs the rules of a kind in this order.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCalculationModelsAndReportsFeature(this IServiceCollection services)
    {
        services.AddScoped<VersionKeys>();

        services.AddScoped<CalculationModelService>();
        services.AddScoped<CalculationModelVersionService>();
        services.AddScoped<ModelVersionCloner>();
        services.AddScoped<ModelKeys>();
        services.AddScoped<ReportUses>();
        services.AddVersionContent<VariableService, VariableRecord, VariableInput>();
        services.AddVersionContent<CalculatedMeasureService, CalculatedMeasureRecord, CalculatedMeasureInput>();

        services.AddScoped<IPublicationRule, FormulaReferencesResolveRule>();
        services.AddScoped<IPublicationRule, FormulasAcyclicRule>();

        services.AddScoped<ReportService>();
        services.AddScoped<ReportVersionService>();
        services.AddScoped<ReportVersionCloner>();
        services.AddScoped<ReportContentRemover>();
        services.AddScoped<ReportVersionNames>();
        services.AddScoped<ReportColumnService>();
        services.AddScoped<ReportLineService>();
        services.AddScoped<ReportMemberOptionService>();
        services.AddScoped<ReportVariableScopeService>();
        services.AddScoped<ModelMetricsReader>();
        services.AddScoped<ReportGraphReader>();
        services.AddVersionContent<VariableSourceService, VariableSourceRecord, VariableSourceInput>();
        services.AddVersionContent<SourceProfileService, SourceProfile, SourceProfileInput>();
        services.AddVersionContent<CalculationDimensionService, CalculationDimensionRecord, CalculationDimensionInput>();

        services.AddScoped<IPublicationRule, ModelVersionPublishedRule>();
        services.AddScoped<IPublicationRule, ReportItemsResolveRule>();
        services.AddScoped<IPublicationRule, CalculationGroupingResolveRule>();
        services.AddScoped<IPublicationRule, DefaultProfileEnabledRule>();
        services.AddScoped<IPublicationRule, ReportDefaultsResolveRule>();
        services.AddScoped<IPublicationRule, ColumnKeysDistinctRule>();
        foreach (var kind in new[] { VersionKinds.ReportVersion, VersionKinds.CalculationModelVersion })
        {
            services.AddRuleFor<ReportMetricsResolveRule>(kind);
            services.AddRuleFor<ProfilesCompleteRule>(kind);
        }
        foreach (var kind in new[] { VersionKinds.ReportVersion, VersionKinds.SemanticRelease })
            services.AddRuleFor<ReportSourcesResolveRule>(kind);
        return services;
    }

    /// <summary>Registers a report rule for one kind; <see cref="ReportGraphReader"/> gives each kind its scope of reports.</summary>
    private static void AddRuleFor<TRule>(this IServiceCollection services, string kind)
        where TRule : IPublicationRule
    {
        services.AddScoped<IPublicationRule>(provider => ActivatorUtilities.CreateInstance<TRule>(provider, kind));
    }

    /// <summary>Registers one version content kind's service and the validator that checks its item routes.</summary>
    private static void AddVersionContent<TService, TItem, TInput>(this IServiceCollection services)
        where TService : class, IVersionContent<TItem, TInput>
    {
        services.AddScoped<IVersionContent<TItem, TInput>, TService>();
        services.AddScoped<IValidator<VersionItemReference<TItem>>, VersionItemReferenceValidator<TItem, TInput>>();
    }
}
