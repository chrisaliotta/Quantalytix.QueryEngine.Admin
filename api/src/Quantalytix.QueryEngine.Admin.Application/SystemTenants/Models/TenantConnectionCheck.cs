namespace Quantalytix.QueryEngine.Admin.Application.SystemTenants;

/// <summary>The result of verifying a tenant connection string, which the tenant editor's "Test connection" button shows.</summary>
public sealed record TenantConnectionCheck
{
    /// <summary>Whether the connection opens a database of the metadata schema version this API supports that belongs to the tenant.</summary>
    public required bool IsValid { get; init; }

    /// <summary>What the connection reached, or why it is rejected.</summary>
    public required string Message { get; init; }
}
