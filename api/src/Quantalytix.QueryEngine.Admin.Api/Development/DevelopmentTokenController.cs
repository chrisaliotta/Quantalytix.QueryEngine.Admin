using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Quantalytix.QueryEngine.Admin.Api.Development;

/// <summary>Issues development bearer tokens. Exists only on a Development host.</summary>
[ApiController]
[Route("development/token")]
[AllowAnonymous]
public sealed class DevelopmentTokenController(DevelopmentTokenIssuer issuer) : ControllerBase
{
    /// <summary>Issues a bearer token for a tenant, optionally with the SystemAdmin role.</summary>
    /// <param name="request">The tenant and role the token carries.</param>
    /// <returns>The signed token, valid for eight hours or until the host restarts.</returns>
    /// <response code="200">The signed token, valid for eight hours or until the host restarts.</response>
    [HttpPost]
    [ProducesResponseType<DevelopmentTokenResponse>(StatusCodes.Status200OK)]
    public ActionResult<DevelopmentTokenResponse> Issue(DevelopmentTokenRequest request)
    {
        return Ok(issuer.Issue(request));
    }
}
