using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.Application.SystemTenants;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.SystemTenants;

/// <summary>
/// Administers the control plane's tenant directory for SystemAdmins. Connection strings are write-only: every response
/// masks the password. Tenants cannot be deleted. The directory is not a tenant table, so its 404s are declared here rather
/// than by the shared route check.
/// </summary>
[ApiController]
[Route("api/system/tenants")]
[Authorize(Policy = AdminPolicies.SystemAdmin)]
[ProducesResponseType<Problem>(StatusCodes.Status401Unauthorized, ProblemWriter.ContentType)]
[ProducesResponseType<Problem>(StatusCodes.Status403Forbidden, ProblemWriter.ContentType)]
public sealed class SystemTenantsController(SystemTenantService tenants, TenantConnectionVerification verification) : ControllerBase
{
    /// <summary>List every tenant in the directory.</summary>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The directory entries with masked connection strings.</returns>
    /// <response code="200">The directory entries, ordered by tenant id, with masked connection strings.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller lacks the SystemAdmin role.</response>
    [HttpGet(Name = "listSystemTenants")]
    [ProducesResponseType<IReadOnlyList<TenantDirectoryEntry>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenantDirectoryEntry>>> List(CancellationToken cancellationToken)
    {
        return Ok(await tenants.ListAsync(cancellationToken));
    }

    /// <summary>Read one tenant's directory entry.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The directory entry with a masked connection string.</returns>
    /// <response code="200">The directory entry with a masked connection string.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller lacks the SystemAdmin role.</response>
    /// <response code="404">The directory has no such tenant.</response>
    [HttpGet("{tenantId}", Name = "getSystemTenant")]
    [ProducesResponseType<TenantDirectoryEntry>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status404NotFound, ProblemWriter.ContentType)]
    public async Task<ActionResult<TenantDirectoryEntry>> Get(string tenantId, CancellationToken cancellationToken)
    {
        return Found(await tenants.GetAsync(tenantId, cancellationToken));
    }

    /// <summary>Register a tenant after verifying its connection string.</summary>
    /// <param name="input">The tenant id, provider, connection string, enabled flag and cache lifetime.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The new directory entry.</returns>
    /// <response code="201">The tenant was registered; the body is its entry with a masked connection string.</response>
    /// <response code="400">A field breaks a rule; a <c>connectionString</c> error gives the reason verification failed.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller lacks the SystemAdmin role.</response>
    [HttpPost(Name = "createSystemTenant")]
    [ProducesResponseType<TenantDirectoryEntry>(StatusCodes.Status201Created)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    public async Task<ActionResult<TenantDirectoryEntry>> Create(TenantCreateInput input, CancellationToken cancellationToken)
    {
        var entry = await tenants.CreateAsync(input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { tenantId = entry.TenantId }, entry);
    }

    /// <summary>Change a tenant's provider, cache lifetime and connection string.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="input">The edit; a null connection string keeps the current one.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The updated directory entry.</returns>
    /// <response code="200">The updated entry with a masked connection string.</response>
    /// <response code="400">A field breaks a rule; a <c>connectionString</c> error gives the reason verification failed.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller lacks the SystemAdmin role.</response>
    /// <response code="404">The directory has no such tenant.</response>
    [HttpPut("{tenantId}", Name = "updateSystemTenant")]
    [ProducesResponseType<TenantDirectoryEntry>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status404NotFound, ProblemWriter.ContentType)]
    public async Task<ActionResult<TenantDirectoryEntry>> Update(string tenantId, TenantUpdateInput input, CancellationToken cancellationToken)
    {
        return Found(await tenants.UpdateAsync(tenantId, input, cancellationToken));
    }

    /// <summary>Permit or stop query routing for a tenant.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="input">Whether routing is permitted.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The updated directory entry.</returns>
    /// <response code="200">The updated entry with a masked connection string.</response>
    /// <response code="400">The body does not bind.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller lacks the SystemAdmin role.</response>
    /// <response code="404">The directory has no such tenant.</response>
    [HttpPut("{tenantId}/enabled", Name = "setSystemTenantEnabled")]
    [ProducesResponseType<TenantDirectoryEntry>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status404NotFound, ProblemWriter.ContentType)]
    public async Task<ActionResult<TenantDirectoryEntry>> SetEnabled(string tenantId, TenantEnabledInput input, CancellationToken cancellationToken)
    {
        return Found(await tenants.SetEnabledAsync(tenantId, input, cancellationToken));
    }

    /// <summary>Test a connection string for a tenant without storing it.</summary>
    /// <param name="input">The tenant and the candidate connection string.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>Whether the connection string is valid, and what was found or why it is rejected.</returns>
    /// <response code="200">The test ran; <c>isValid</c> and <c>message</c> give its outcome.</response>
    /// <response code="400">The tenant id or the connection string is missing or too long.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller lacks the SystemAdmin role.</response>
    [HttpPost("verify", Name = "verifySystemTenantConnection")]
    [ProducesResponseType<TenantConnectionCheck>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    public async Task<ActionResult<TenantConnectionCheck>> Verify(TenantVerifyInput input, CancellationToken cancellationToken)
    {
        return Ok(await verification.CheckAsync(input.TenantId, input.ConnectionString, cancellationToken));
    }

    /// <summary>The entry, or the 404 problem when the directory has no such tenant.</summary>
    private ActionResult<TenantDirectoryEntry> Found(TenantDirectoryEntry? entry)
    {
        return entry is null ? Problems.OfStatus(StatusCodes.Status404NotFound).ToResult() : Ok(entry);
    }
}
