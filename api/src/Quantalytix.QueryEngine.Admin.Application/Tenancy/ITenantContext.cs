namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>The tenant the current request administers. Tenant table gateways open that tenant's database.</summary>
public interface ITenantContext
{
    /// <summary>The <c>qe_tenant_directory.tenant_id</c> of the tenant, already authorized for the caller.</summary>
    string TenantId { get; }
}
