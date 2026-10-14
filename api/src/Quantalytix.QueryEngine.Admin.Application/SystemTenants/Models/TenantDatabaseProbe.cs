using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.SystemTenants;

/// <summary>What a candidate tenant connection string reached: the database's info row, or why it could not be read.</summary>
public sealed record TenantDatabaseProbe
{
    /// <summary>The database's <c>qe_database_info</c> row; null when the probe failed.</summary>
    public DatabaseInfoRecord? DatabaseInfo { get; init; }

    /// <summary>Why the connection could not be opened or the row read; null when the probe succeeded.</summary>
    public string? Failure { get; init; }
}
