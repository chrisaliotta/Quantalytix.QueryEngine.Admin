using Quantalytix.QueryEngine.Admin.Application.SystemTenants;

namespace Quantalytix.QueryEngine.Admin.Tests.SystemTenants;

/// <summary>A tenant database verifier that answers every probe with a fixed result and records the connection strings it was given.</summary>
/// <param name="probe">The result every probe returns.</param>
internal sealed class FakeTenantDatabaseVerifier(TenantDatabaseProbe probe) : ITenantDatabaseVerifier
{
    /// <summary>The connection strings probed, in order.</summary>
    public List<string> ProbedConnectionStrings { get; } = [];

    /// <summary>Records the connection string and returns the fixed probe result.</summary>
    /// <param name="connectionString">The candidate connection string.</param>
    /// <param name="cancellationToken">Unused; the fake answers synchronously.</param>
    /// <returns>The fixed probe result.</returns>
    public Task<TenantDatabaseProbe> ProbeAsync(string connectionString, CancellationToken cancellationToken)
    {
        ProbedConnectionStrings.Add(connectionString);
        return Task.FromResult(probe);
    }
}
