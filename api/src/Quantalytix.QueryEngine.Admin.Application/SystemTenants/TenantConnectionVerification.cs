namespace Quantalytix.QueryEngine.Admin.Application.SystemTenants;

/// <summary>
/// Verifies that a connection string routes a tenant to its own metadata database: the connection opens, the database's
/// <c>qe_database_info</c> names the tenant and has the metadata schema version this API reads and writes, and exactly one
/// assumption set is active, which every publish repoints. A connection string is external input whose outcome the API cannot
/// predict, so it is verified whenever one is supplied.
/// </summary>
public sealed class TenantConnectionVerification(ITenantDatabaseVerifier verifier)
{
    /// <summary>The metadata schema version this API reads and writes.</summary>
    public const int SupportedSchemaVersion = 15;

    /// <summary>Opens the connection string and checks the database it reaches.</summary>
    /// <param name="tenantId">The tenant the connection string must route to.</param>
    /// <param name="connectionString">The candidate connection string.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>Whether the connection string is valid for the tenant, and what was found or why it is rejected.</returns>
    public async Task<TenantConnectionCheck> CheckAsync(string tenantId, string connectionString, CancellationToken cancellationToken)
    {
        var probe = await verifier.ProbeAsync(connectionString, cancellationToken);
        if (probe.Failure is not null) return Invalid(probe.Failure);

        var info = probe.DatabaseInfo!;
        if (info.TenantKey != tenantId)
            return Invalid($"The database belongs to tenant '{info.TenantKey}', not '{tenantId}'.");
        if (info.MetadataSchemaVersion != SupportedSchemaVersion)
            return Invalid($"The database has metadata schema version {info.MetadataSchemaVersion}; this API requires version {SupportedSchemaVersion}.");
        if (probe.ActiveAssumptionSets != 1)
            return Invalid($"The database has {probe.ActiveAssumptionSets} active assumption sets; it needs exactly one, which holds the default versions.");
        return new TenantConnectionCheck
        {
            IsValid = true,
            Message = $"Connected to tenant '{info.TenantKey}' (metadata schema version {info.MetadataSchemaVersion}, semantic revision {info.SemanticRevision})."
        };
    }

    /// <summary>A failed check with the reason.</summary>
    private static TenantConnectionCheck Invalid(string message)
    {
        return new TenantConnectionCheck { IsValid = false, Message = message };
    }
}
