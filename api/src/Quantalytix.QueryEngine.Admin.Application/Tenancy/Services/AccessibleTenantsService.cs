using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>Lists the tenants a caller may administer from the control plane's tenant directory.</summary>
public sealed class AccessibleTenantsService(ITableGateway<TenantDirectoryRecord> tenants)
{
    /// <summary>Lists every tenant for a system administrator, and only the caller's own tenant for anyone else.</summary>
    /// <param name="caller">The authenticated caller.</param>
    /// <param name="cancellationToken">Cancels the directory read.</param>
    /// <returns>The accessible tenants, sorted by tenant ID.</returns>
    public async Task<IReadOnlyList<AccessibleTenant>> ListAsync(Caller caller, CancellationToken cancellationToken)
    {
        object? filter = null;
        if (!caller.IsSystemAdmin) filter = new { caller.TenantId };
        var rows = await tenants.ListAsync(filter, cancellationToken);
        return
        [
            .. rows
                .OrderBy(row => row.TenantId, StringComparer.Ordinal)
                .Select(row => new AccessibleTenant { TenantId = row.TenantId, IsEnabled = row.IsEnabled })
        ];
    }
}
