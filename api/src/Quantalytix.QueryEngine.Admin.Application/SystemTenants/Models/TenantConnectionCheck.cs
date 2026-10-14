namespace Quantalytix.QueryEngine.Admin.Application.SystemTenants;

/// <summary>The result of verifying a tenant connection string, which the tenant editor's "Test connection" button shows.</summary>
public sealed record TenantConnectionCheck
{
    /// <summary>Whether the connection opens a metadata schema version 4 database that belongs to the tenant.</summary>
    public required bool IsValid { get; init; }

    /// <summary>What the connection reached, or why it is rejected.</summary>
    public required string Message { get; init; }
}
