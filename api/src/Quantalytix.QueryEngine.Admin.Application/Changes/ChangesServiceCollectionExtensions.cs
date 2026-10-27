using Microsoft.Extensions.DependencyInjection;

namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>How a module registers an aggregate and its rules with the change-set pipeline, from its own DI extension.</summary>
public static class ChangesServiceCollectionExtensions
{
    /// <summary>Registers an aggregate and the pipeline that runs it, both scoped.</summary>
    /// <typeparam name="TAggregate">The module's implementation.</typeparam>
    /// <typeparam name="TSnapshot">The aggregate's in-memory content.</typeparam>
    /// <typeparam name="TOperation">The aggregate's operation union.</typeparam>
    /// <typeparam name="TState">The content read model.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddChangeSetAggregate<TAggregate, TSnapshot, TOperation, TState>(this IServiceCollection services)
        where TAggregate : class, IChangeSetAggregate<TSnapshot, TOperation, TState>
        where TOperation : notnull
        where TState : notnull
    {
        services.AddScoped<IChangeSetAggregate<TSnapshot, TOperation, TState>, TAggregate>();
        services.AddScoped<ChangeSetPipeline<TSnapshot, TOperation, TState>>();
        return services;
    }

    /// <summary>Registers a snapshot rule of an aggregate, scoped; rules run in registration order.</summary>
    /// <typeparam name="TSnapshot">The aggregate's in-memory content.</typeparam>
    /// <typeparam name="TRule">The rule.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSnapshotRule<TSnapshot, TRule>(this IServiceCollection services)
        where TRule : class, ISnapshotRule<TSnapshot>
    {
        return services.AddScoped<ISnapshotRule<TSnapshot>, TRule>();
    }

    /// <summary>Runs a version kind's existing publication rules as the snapshot's publication rules, until they migrate.</summary>
    /// <typeparam name="TSnapshot">A snapshot of one version.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddVersionRuleAdapter<TSnapshot>(this IServiceCollection services) where TSnapshot : IVersionSnapshot
    {
        return services.AddSnapshotRule<TSnapshot, VersionRuleAdapter<TSnapshot>>();
    }
}
