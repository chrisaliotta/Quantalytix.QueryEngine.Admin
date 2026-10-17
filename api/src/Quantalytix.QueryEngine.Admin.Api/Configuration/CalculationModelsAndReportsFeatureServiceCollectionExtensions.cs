using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Api.Configuration;

/// <summary>Registration hook owned by the Calculation models and Reports slice (models, model versions and their content, reports and report versions).</summary>
public static class CalculationModelsAndReportsFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the slice's services, its <c>calculation_model_version</c> and <c>report_version</c> publication rules and the
    /// lifecycle handlers that move the model's and the report's current-version pointers, all scoped. Each model content kind
    /// is registered under <see cref="IModelVersionContent{TItem, TInput}"/> with the validator of its item routes. The
    /// workflow runs the rules of a kind in this order.
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

        services.AddScoped<IVersionLifecycleHandler, CurrentModelVersionUpdater>();

        services.AddScoped<IPublicationRule, FormulaReferencesResolveRule>();
        services.AddScoped<IPublicationRule, FormulasAcyclicRule>();
        services.AddScoped<IPublicationRule, ProfilesCompleteRule>();

        services.AddScoped<ReportService>();
        services.AddScoped<ReportVersionService>();
        services.AddScoped<ReportVersionCloner>();
        services.AddScoped<ReportColumnService>();
        services.AddScoped<ReportLineService>();
        services.AddScoped<ModelMetricsReader>();

        services.AddScoped<IVersionLifecycleHandler, CurrentReportVersionUpdater>();

        services.AddScoped<IPublicationRule, ModelVersionPublishedRule>();
        services.AddScoped<IPublicationRule, DefaultProfileEnabledRule>();
        services.AddScoped<IPublicationRule, ReportDefaultsResolveRule>();
        services.AddScoped<IPublicationRule, ColumnKeysDistinctRule>();
        return services;
    }

    /// <summary>Registers one model content kind's service and the validator that checks its item routes.</summary>
    private static void AddModelContent<TService, TItem, TInput>(this IServiceCollection services)
        where TService : class, IModelVersionContent<TItem, TInput>
    {
        services.AddScoped<IModelVersionContent<TItem, TInput>, TService>();
        services.AddScoped<IValidator<ModelItemReference<TItem>>, ModelItemReferenceValidator<TItem, TInput>>();
    }
}
