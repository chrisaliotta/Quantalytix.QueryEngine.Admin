using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Session;

/// <summary>Describes the signed-in caller.</summary>
[ApiController]
[Route("api/me")]
public sealed class SessionController(CallerFactory callers) : ControllerBase
{
    /// <summary>Read who the caller is.</summary>
    /// <returns>The caller's session.</returns>
    /// <response code="200">The caller as the API sees its bearer token.</response>
    [HttpGet(Name = "getSession")]
    [ProducesResponseType<SessionInfo>(StatusCodes.Status200OK)]
    public ActionResult<SessionInfo> Get()
    {
        var caller = callers.Create(User);
        return Ok(new SessionInfo
        {
            Subject = caller.Subject,
            DisplayName = User.FindFirstValue(JwtRegisteredClaimNames.Name),
            TenantId = caller.TenantId,
            IsSystemAdmin = caller.IsSystemAdmin
        });
    }
}
