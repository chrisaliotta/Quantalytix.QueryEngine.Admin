using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.SystemTenants;

/// <summary>
/// Administers the control plane's tenant directory (<c>query_control.qe_tenant_directory</c>) for SystemAdmins. Every
/// entry it returns has its connection string's password masked; tenants are never deleted.
/// </summary>
public sealed class SystemTenantService(ITableGateway<TenantDirectoryRecord> tenants, IConnectionStringMasker masker, TimeProvider time)
{
    /// <summary>Lists every tenant, ordered by tenant ID.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The masked directory entries.</returns>
    public async Task<IReadOnlyList<TenantDirectoryEntry>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await tenants.ListAsync(null, cancellationToken);
        return [.. rows.OrderBy(row => row.TenantId, StringComparer.Ordinal).Select(ToEntry)];
    }

    /// <summary>Reads one tenant.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The masked directory entry.</returns>
    public async Task<TenantDirectoryEntry> GetAsync(string tenantId, CancellationToken cancellationToken)
    {
        return ToEntry(await tenants.GetAsync(new { TenantId = tenantId }, cancellationToken));
    }

    /// <summary>Registers a tenant.</summary>
    /// <param name="input">The validated tenant, whose connection string was verified.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The masked directory entry.</returns>
    public async Task<TenantDirectoryEntry> CreateAsync(TenantCreateInput input, CancellationToken cancellationToken)
    {
        var row = new TenantDirectoryRecord
        {
            TenantId = input.TenantId,
            ProviderId = input.ProviderId,
            ConnectionString = input.ConnectionString,
            IsEnabled = input.IsEnabled,
            MetadataCacheSeconds = input.MetadataCacheSeconds,
            UpdatedAt = time.GetUtcNow().UtcDateTime
        };
        await tenants.InsertAsync(row, cancellationToken);
        return ToEntry(row);
    }

    /// <summary>Changes a tenant's provider, cache lifetime and, when a new one is given, its connection string.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="input">The validated edit; a null connection string keeps the current one.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The masked directory entry.</returns>
    public async Task<TenantDirectoryEntry> UpdateAsync(string tenantId, TenantUpdateInput input, CancellationToken cancellationToken)
    {
        var current = await tenants.GetAsync(new { TenantId = tenantId }, cancellationToken);
        var updated = current with
        {
            ProviderId = input.ProviderId,
            ConnectionString = input.ConnectionString ?? current.ConnectionString,
            MetadataCacheSeconds = input.MetadataCacheSeconds,
            UpdatedAt = time.GetUtcNow().UtcDateTime
        };
        await tenants.UpdateAsync(updated, cancellationToken);
        return ToEntry(updated);
    }

    /// <summary>Permits or stops query routing for a tenant.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="input">Whether routing is permitted.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The masked directory entry.</returns>
    public async Task<TenantDirectoryEntry> SetEnabledAsync(string tenantId, TenantEnabledInput input, CancellationToken cancellationToken)
    {
        var current = await tenants.GetAsync(new { TenantId = tenantId }, cancellationToken);
        var updated = current with { IsEnabled = input.IsEnabled, UpdatedAt = time.GetUtcNow().UtcDateTime };
        await tenants.UpdateAsync(updated, cancellationToken);
        return ToEntry(updated);
    }

    /// <summary>A directory row as the API returns it, with the connection string masked.</summary>
    private TenantDirectoryEntry ToEntry(TenantDirectoryRecord row)
    {
        return new TenantDirectoryEntry
        {
            TenantId = row.TenantId,
            ProviderId = row.ProviderId,
            ConnectionString = masker.Mask(row.ConnectionString),
            IsEnabled = row.IsEnabled,
            MetadataCacheSeconds = row.MetadataCacheSeconds,
            UpdatedAt = row.UpdatedAt
        };
    }
}
