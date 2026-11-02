using Microsoft.Extensions.DependencyInjection;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Configuration;

/// <summary>Registers the application services every feature slice builds on.</summary>
public static class AdminApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers tenant listing, the live snapshot every loader resolves versions in, the frozen holders every delete check names, the
    /// semantic revision and the snapshot rule runner, all scoped to the request. The API registers the FluentValidation validators
    /// by scanning this assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAdminApplication(this IServiceCollection services)
    {
        services.AddScoped<AccessibleTenantsService>();
        services.AddScoped<LiveSnapshot>();
        services.AddScoped<SnapshotHolders>();
        services.AddScoped<SemanticRevision>();
        services.AddScoped(typeof(SnapshotRuleRunner<>));
        return services;
    }
}
