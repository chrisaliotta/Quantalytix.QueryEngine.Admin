using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Configuration;
using Quantalytix.QueryEngine.Admin.Application.Configuration;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Configuration;

namespace Quantalytix.QueryEngine.Admin.Tests.Support;

/// <summary>
/// The development tenant for the opt-in live tests: the API's StarRocks infrastructure, application services and features, as
/// the API registers them, scoped to the tenant. Opt in with <c>QE_ADMIN_LIVE_GATEWAY=true</c>; the control-plane connection
/// comes from the API's user secrets or <c>ConnectionStrings__ControlPlane</c>.
/// </summary>
internal static class LiveTenant
{
    /// <summary>The tenant the live tests read and write.</summary>
    public const string TenantId = "development";

    /// <summary>Whether the live tests run.</summary>
    public static bool IsEnabled
    {
        get
        {
            return string.Equals(Environment.GetEnvironmentVariable("QE_ADMIN_LIVE_GATEWAY"), "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>The services of the development tenant.</summary>
    /// <returns>A provider whose scopes resolve services as a request to the tenant would.</returns>
    public static ServiceProvider Services()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(AdminPolicies).Assembly)
            .AddEnvironmentVariables()
            .Build();
        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddAdminApplication()
            .AddStarRocksInfrastructure(configuration)
            .AddDatasetsFeature()
            .AddDimensionsFeature()
            .AddMappingsAndBridgesFeature()
            .AddBindingsAndMeasuresFeature()
            .AddReleasesAndSystemFeature()
            .AddCalculationModelsAndReportsFeature()
            .AddSingleton<ITenantContext>(new FixedTenantContext(TenantId))
            .BuildServiceProvider();
    }
}
