namespace Quantalytix.QueryEngine.Admin.Application.SystemTenants;

/// <summary>A connection string to test for a tenant, without storing it.</summary>
public sealed record TenantVerifyInput
{
    /// <summary>The tenant the connection string must route to.</summary>
    public required string TenantId { get; init; }

    /// <summary>The candidate connection string.</summary>
    public required string ConnectionString { get; init; }
}
