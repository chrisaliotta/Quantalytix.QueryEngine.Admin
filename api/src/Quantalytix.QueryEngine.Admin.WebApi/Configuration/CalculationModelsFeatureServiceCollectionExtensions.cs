using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.WebApi.Routing;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registration hook owned by the Calculation models slice.</summary>
public static class CalculationModelsFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Calculation models slice, all scoped: its route resource, its reads and commands, the readers of parameters and
    /// attributions the report slice shares, and the model change-set aggregate with its gate, which refuses an introduced violation,
    /// and its findings, which list every violation the pending model has.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCalculationModelsFeature(this IServiceCollection services)
    {
        services.AddRouteResource<CalculationModelRecord>("calculation-models", id => new { CalculationModelId = id });

        services.AddScoped<ModelQueries>();
        services.AddScoped<ModelCommands>();
        services.AddScoped<ModelSnapshotLoader>();
        services.AddScoped<MetricAttributionService>();
        services.AddScoped<ModelParameterService>();

        services.AddChangeSetAggregate<ModelChanges, ModelSnapshot, ModelOperation, ModelDocument>();
        services.AddSnapshotRule<ModelSnapshot, ModelGateRule>();
        services.AddSnapshotRule<ModelSnapshot, ModelFindingsRule>();
        return services;
    }
}
