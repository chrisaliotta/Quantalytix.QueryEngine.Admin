using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Session;

/// <summary>Describes the signed-in caller.</summary>
[ApiController]
[Route("api/me")]
[ProducesResponseType<Problem>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class SessionController(CallerFactory callers) : ControllerBase
{
    /// <summary>Returns the caller's subject, tenant and SystemAdmin role.</summary>
    /// <returns>The caller's session.</returns>
    /// <response code="200">The caller's session.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    [HttpGet]
    [ProducesResponseType<SessionInfo>(StatusCodes.Status200OK)]
    public ActionResult<SessionInfo> Get()
    {
        var caller = callers.Create(User);
        return Ok(new SessionInfo { Subject = caller.Subject, TenantId = caller.TenantId, IsSystemAdmin = caller.IsSystemAdmin });
    }
}
