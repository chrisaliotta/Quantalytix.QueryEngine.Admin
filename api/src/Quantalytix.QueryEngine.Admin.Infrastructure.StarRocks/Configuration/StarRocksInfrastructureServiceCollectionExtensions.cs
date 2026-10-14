using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Connections;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Identifiers;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Tables;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Configuration;

/// <summary>Registers the StarRocks implementations of the application's persistence ports.</summary>
public static class StarRocksInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Maps snake_case columns onto record properties, binds <see cref="StarRocksOptions"/>, and registers the scoped
    /// connection factory, a scoped table gateway for every record, the ID generator and the system clock.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration holding <c>ConnectionStrings:ControlPlane</c> and the <c>StarRocks</c> section.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddStarRocksInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        services.AddOptions<StarRocksOptions>().Bind(configuration.GetSection(StarRocksOptions.SectionName));
        services.AddScoped<IStarRocksConnectionFactory, StarRocksConnectionFactory>();
        services.AddScoped(typeof(ITableGateway<>), typeof(StarRocksTableGateway<>));
        services.AddSingleton<IIdGenerator, IdGenerator>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
