using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.SystemTenants;

/// <summary>
/// Administers the control plane's tenant directory (<c>query_control.qe_tenant_directory</c>) for SystemAdmins. Every
/// entry it returns has its connection string's password masked; tenants are never deleted. The directory lies outside the
/// tenant tables the shared route check reads, so the reads and edits of one tenant answer null for a tenant it does not hold.
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
    /// <returns>The masked directory entry, or null when the directory has no such tenant.</returns>
    public async Task<TenantDirectoryEntry?> GetAsync(string tenantId, CancellationToken cancellationToken)
    {
        var row = await FindAsync(tenantId, cancellationToken);
        return row is null ? null : ToEntry(row);
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
    /// <returns>The masked directory entry, or null when the directory has no such tenant.</returns>
    public Task<TenantDirectoryEntry?> UpdateAsync(string tenantId, TenantUpdateInput input, CancellationToken cancellationToken)
    {
        return ChangeAsync(tenantId, current => current with
        {
            ProviderId = input.ProviderId,
            ConnectionString = input.ConnectionString ?? current.ConnectionString,
            MetadataCacheSeconds = input.MetadataCacheSeconds
        }, cancellationToken);
    }

    /// <summary>Permits or stops query routing for a tenant.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="input">Whether routing is permitted.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The masked directory entry, or null when the directory has no such tenant.</returns>
    public Task<TenantDirectoryEntry?> SetEnabledAsync(string tenantId, TenantEnabledInput input, CancellationToken cancellationToken)
    {
        return ChangeAsync(tenantId, current => current with { IsEnabled = input.IsEnabled }, cancellationToken);
    }

    /// <summary>Applies a change to a tenant's row, stamps it and writes it.</summary>
    private async Task<TenantDirectoryEntry?> ChangeAsync(string tenantId, Func<TenantDirectoryRecord, TenantDirectoryRecord> change, CancellationToken cancellationToken)
    {
        var current = await FindAsync(tenantId, cancellationToken);
        if (current is null) return null;
        var updated = change(current) with { UpdatedAt = time.GetUtcNow().UtcDateTime };
        await tenants.UpdateAsync(updated, cancellationToken);
        return ToEntry(updated);
    }

    /// <summary>The tenant's row, or null when the directory has no such tenant.</summary>
    private async Task<TenantDirectoryRecord?> FindAsync(string tenantId, CancellationToken cancellationToken)
    {
        return (await tenants.ListAsync(new { TenantId = tenantId }, cancellationToken)).SingleOrDefault();
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
