using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>
/// Reads the tenant's <see cref="TenantInfo"/> from the singleton <c>qe_database_info</c> row. The overview, the catalog and the
/// release activation answer (blueprint 14) all report it.
/// </summary>
public sealed class TenantInfoReader(ITableGateway<DatabaseInfoRecord> databaseInfo)
{
    /// <summary>The key of the singleton row.</summary>
    private const int SingletonId = 1;

    /// <summary>Reads the tenant's identity, revision and active release.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The tenant info.</returns>
    public async Task<TenantInfo> ReadAsync(CancellationToken cancellationToken)
    {
        var row = await databaseInfo.GetAsync(new { SingletonId }, cancellationToken);
        return new TenantInfo
        {
            TenantKey = row.TenantKey,
            MetadataSchemaVersion = row.MetadataSchemaVersion,
            SemanticRevision = row.SemanticRevision,
            ActiveReleaseId = row.ActiveSemanticReleaseId
        };
    }
}
