using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Api.Versioning;

/// <summary>Moves dataset, dimension, mapping and bridge versions, semantic releases and assumption sets through their lifecycle.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/versions")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class VersionStatusController(VersionWorkflow workflow) : ControllerBase
{
    /// <summary>
    /// Changes a version's status. Leaving draft runs the kind's publication rules; publishing stamps the publication time.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="version">The version's kind and ID.</param>
    /// <param name="input">The target status.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The version's new state; 409 when the lifecycle forbids the transition; 422 with the violations when rules reject it.</returns>
    /// <response code="200">The transition applied; the body is the version's new state.</response>
    /// <response code="400">The kind or the target status is not a known value.</response>
    /// <response code="409">The lifecycle does not allow moving from the current status to the target; the detail names both.</response>
    /// <response code="422">Publication rules rejected the version and its status is unchanged; <c>violations</c> lists why.</response>
    [HttpPut("{kind}/{id:long}/status")]
    [ProducesResponseType<VersionState>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<VersionState>> ChangeStatus(
        string tenantId,
        [FromRoute] VersionReference version,
        StatusChangeInput input,
        CancellationToken cancellationToken)
    {
        var result = await workflow.TransitionAsync(version.Kind, version.Id, input.Status, cancellationToken);
        if (result.Outcome == TransitionOutcome.NotAllowed)
        {
            var detail = $"{version.Kind} {version.Id} is {result.State.Status}; it cannot move to {input.Status}.";
            return Problem(detail, statusCode: StatusCodes.Status409Conflict, title: "Transition not allowed");
        }

        if (result.Outcome == TransitionOutcome.Rejected)
            return RuleViolationResults.Create(HttpContext, $"{version.Kind} {version.Id} cannot become {input.Status}.", result.Violations);
        return Ok(result.State);
    }
}
