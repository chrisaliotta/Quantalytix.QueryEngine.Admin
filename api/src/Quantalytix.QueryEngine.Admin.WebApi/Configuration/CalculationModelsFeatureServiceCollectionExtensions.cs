using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.WebApi.Routing;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registration hook owned by the Calculation models slice.</summary>
public static class CalculationModelsFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Calculation models slice, all scoped: its route resources, its reads and commands, the readers of parameters and
    /// attributions the report slice shares, the model change-set aggregate with its gate, which refuses an introduced violation, and
    /// its findings, which list every violation the pending model has, and the kind's version rules, copier and remover that snapshots
    /// use.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCalculationModelsFeature(this IServiceCollection services)
    {
        services.AddRouteResource<CalculationModelRecord>("calculation-models", id => new { CalculationModelId = id });
        services.AddRouteResource<CalculationModelVersionRecord>("calculation-model-versions", id => new { CalculationModelVersionId = id });

        services.AddScoped<ModelQueries>();
        services.AddScoped<ModelCommands>();
        services.AddScoped<ModelSnapshotLoader>();
        services.AddScoped<MetricAttributionService>();
        services.AddScoped<ModelParameterService>();
        services.AddScoped<CalculationModelVersionRemover>();
        services.AddScoped<IVersionRemover>(provider => provider.GetRequiredService<CalculationModelVersionRemover>());
        services.AddScoped<IVersionCopier, CalculationModelVersionCopier>();
        services.AddScoped<IVersionRules, CalculationModelVersionRules>();

        services.AddChangeSetAggregate<ModelChanges, ModelSnapshot, ModelOperation, ModelDocument>();
        services.AddSnapshotRule<ModelSnapshot, ModelGateRule>();
        services.AddSnapshotRule<ModelSnapshot, ModelFindingsRule>();
        return services;
    }
}
