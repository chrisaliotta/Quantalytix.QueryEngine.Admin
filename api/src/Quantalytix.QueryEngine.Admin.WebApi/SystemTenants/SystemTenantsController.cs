using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.Application.SystemTenants;

namespace Quantalytix.QueryEngine.Admin.WebApi.SystemTenants;

/// <summary>
/// Administers the control plane's tenant directory for SystemAdmins. Connection strings are write-only: every response
/// masks the password. Tenants cannot be deleted.
/// </summary>
[ApiController]
[Route("api/system/tenants")]
[Authorize(Policy = AdminPolicies.SystemAdmin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class SystemTenantsController(SystemTenantService tenants, TenantConnectionVerification verification) : ControllerBase
{
    /// <summary>Lists every tenant in the directory, ordered by tenant ID.</summary>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The directory entries with masked connection strings.</returns>
    /// <response code="200">The directory entries, ordered by tenant ID, with masked connection strings.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TenantDirectoryEntry>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenantDirectoryEntry>>> List(CancellationToken cancellationToken)
    {
        return Ok(await tenants.ListAsync(cancellationToken));
    }

    /// <summary>Reads one tenant's directory entry.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The directory entry with a masked connection string.</returns>
    /// <response code="200">The directory entry with a masked connection string.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller lacks the SystemAdmin role.</response>
    [HttpGet("{tenantId}")]
    [ProducesResponseType<TenantDirectoryEntry>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantDirectoryEntry>> Get(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await tenants.GetAsync(tenantId, cancellationToken));
    }

    /// <summary>Registers a tenant after verifying that its connection string routes to the tenant's own metadata database.</summary>
    /// <param name="input">The tenant ID, provider, connection string, enabled flag and cache lifetime.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The new directory entry.</returns>
    /// <response code="201">The tenant was registered; the body is its entry with a masked connection string.</response>
    /// <response code="400">A field breaks a rule; a <c>connectionString</c> error gives the reason verification failed.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller lacks the SystemAdmin role.</response>
    [HttpPost]
    [ProducesResponseType<TenantDirectoryEntry>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<TenantDirectoryEntry>> Create(TenantCreateInput input, CancellationToken cancellationToken)
    {
        var entry = await tenants.CreateAsync(input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { tenantId = entry.TenantId }, entry);
    }

    /// <summary>Changes a tenant's provider, cache lifetime and, when one is given, its verified connection string.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="input">The edit; a null connection string keeps the current one.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The updated directory entry.</returns>
    /// <response code="200">The updated entry with a masked connection string.</response>
    /// <response code="400">A field breaks a rule; a <c>connectionString</c> error gives the reason verification failed.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller lacks the SystemAdmin role.</response>
    [HttpPut("{tenantId}")]
    [ProducesResponseType<TenantDirectoryEntry>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<TenantDirectoryEntry>> Update(string tenantId, TenantUpdateInput input, CancellationToken cancellationToken)
    {
        return Ok(await tenants.UpdateAsync(tenantId, input, cancellationToken));
    }

    /// <summary>Permits or stops query routing for a tenant.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="input">Whether routing is permitted.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The updated directory entry.</returns>
    /// <response code="200">The updated entry with a masked connection string.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller lacks the SystemAdmin role.</response>
    [HttpPut("{tenantId}/enabled")]
    [ProducesResponseType<TenantDirectoryEntry>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantDirectoryEntry>> SetEnabled(string tenantId, TenantEnabledInput input, CancellationToken cancellationToken)
    {
        return Ok(await tenants.SetEnabledAsync(tenantId, input, cancellationToken));
    }

    /// <summary>
    /// Tests a connection string for a tenant without storing it: it must open, and its <c>qe_database_info</c> must name
    /// the tenant and have the metadata schema version this API supports.
    /// </summary>
    /// <param name="input">The tenant and the candidate connection string.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>Whether the connection string is valid, and what was found or why it is rejected.</returns>
    /// <response code="200">The test ran; <c>isValid</c> and <c>message</c> give its outcome.</response>
    /// <response code="400">The tenant ID or the connection string is missing or too long.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller lacks the SystemAdmin role.</response>
    [HttpPost("verify")]
    [ProducesResponseType<TenantConnectionCheck>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<TenantConnectionCheck>> Verify(TenantVerifyInput input, CancellationToken cancellationToken)
    {
        return Ok(await verification.CheckAsync(input.TenantId, input.ConnectionString, cancellationToken));
    }
}
