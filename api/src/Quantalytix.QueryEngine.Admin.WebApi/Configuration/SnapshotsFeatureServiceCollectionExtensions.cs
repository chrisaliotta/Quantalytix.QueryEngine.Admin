using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.WebApi.Routing;

namespace Quantalytix.QueryEngine.Admin.WebApi.Configuration;

/// <summary>Registration hook owned by the Snapshots slice.</summary>
public static class SnapshotsFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Snapshots slice, all scoped: its route resource, the index of which version every snapshot holds, and its reads
    /// and commands, which reach every kind through the copiers, removers and version rules the kinds' slices register.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSnapshotsFeature(this IServiceCollection services)
    {
        services.AddRouteResource<SnapshotRecord>("snapshots", id => new { SnapshotId = id });
        services.AddScoped<SnapshotIndex>();
        services.AddScoped<SnapshotQueries>();
        services.AddScoped<SnapshotCommands>();
        return services;
    }
}
