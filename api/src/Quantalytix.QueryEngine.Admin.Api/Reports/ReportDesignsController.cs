using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.ReportDesigns;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Reports;

/// <summary>
/// Reads and edits a report version as one design: the rows of its report dimension's paired version and its overlay rows, the
/// sources that classify into the rows through the report's own bridges, and the source items mapped to each row. The design's
/// status changes move the report version with its report dimension's version and its bridges' versions, which validate and
/// publish together. Each write answers the design afterwards; writes to the published design are live.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/report-versions/{versionId:long}/design")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ReportDesignsController(
    ReportDesignReader reader,
    ReportDesignVersions designs,
    DesignRowService rows,
    DesignSourceService sources,
    RowMappingService rowMappings,
    DesignCoverageReader coverage,
    VersionWorkflow workflow) : ControllerBase
{
    /// <summary>Reads a report version's design.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="versionId">A draft, validated or published report version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The design.</returns>
    /// <response code="200">The design.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<ReportDesign>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportDesign>> Get(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await reader.ReadAsync(versionId, cancellationToken));
    }

    /// <summary>
    /// Replaces the design's row tree: member rows become members of the report dimension's paired version, overlay rows the
    /// version's overlay rows, and a row the tree leaves out is deleted after its row mappings, variable scopes and member option.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="versionId">A draft or published report version.</param>
    /// <param name="input">The complete row tree.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The design afterwards.</returns>
    /// <response code="200">The design afterwards.</response>
    /// <response code="400">The tree breaks a rule, such as a member row under an overlay row or a removed row a mapping set names; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPut("rows")]
    [RequireEditable(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType<ReportDesign>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<ReportDesign>> ReplaceRows(string tenantId, long versionId, DesignRowsInput input, CancellationToken cancellationToken)
    {
        return Ok(await rows.ReplaceAsync(versionId, input, cancellationToken));
    }

    /// <summary>
    /// Creates a source: a bridge from the role's dimension into the report dimension, optionally with the published mappings of
    /// another bridge from that dimension, and one variable source per variable, which every profile without one picks.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="versionId">A draft or published report version.</param>
    /// <param name="input">The new source.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The design afterwards.</returns>
    /// <response code="200">The design afterwards.</response>
    /// <response code="400">The input breaks a field rule, such as a role the dataset does not bind; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPost("sources")]
    [RequireEditable(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType<ReportDesign>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<ReportDesign>> CreateSource(string tenantId, long versionId, NewDesignSourceInput input, CancellationToken cancellationToken)
    {
        return Ok(await sources.CreateAsync(versionId, input, cancellationToken));
    }

    /// <summary>Edits a source's label, dataset, role, time field, filter and variables; its key and bound dimension stay.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="versionId">A draft or published report version.</param>
    /// <param name="bridgeId">The source's bridge.</param>
    /// <param name="input">The source's new attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The design afterwards.</returns>
    /// <response code="200">The design afterwards.</response>
    /// <response code="400">The input breaks a field rule, such as a role of another dimension; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPut("sources/{bridgeId:long}")]
    [RequireEditable(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType<ReportDesign>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<ReportDesign>> UpdateSource(
        string tenantId,
        long versionId,
        long bridgeId,
        DesignSourceInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await sources.UpdateAsync(versionId, bridgeId, input, cancellationToken));
    }

    /// <summary>Deletes a source's variable sources with the profile picks that name them, and its bridge when only drafts of it exist.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="versionId">A draft or published report version.</param>
    /// <param name="bridgeId">The source's bridge.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The design afterwards.</returns>
    /// <response code="200">The design afterwards.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpDelete("sources/{bridgeId:long}")]
    [RequireEditable(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType<ReportDesign>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportDesign>> DeleteSource(string tenantId, long versionId, long bridgeId, CancellationToken cancellationToken)
    {
        return Ok(await sources.DeleteAsync(versionId, bridgeId, cancellationToken));
    }

    /// <summary>Lists the items of a source that map to no row, which the report leaves out.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="versionId">A draft, validated or published report version.</param>
    /// <param name="bridgeId">The source's bridge.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The unmapped member keys of the source's bound dimension.</returns>
    /// <response code="200">The assignable members of the published version of the source's bound dimension that map to no row, in ordinal order.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet("sources/{bridgeId:long}/coverage")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> Coverage(string tenantId, long versionId, long bridgeId, CancellationToken cancellationToken)
    {
        return Ok(await coverage.UnmappedAsync(versionId, bridgeId, cancellationToken));
    }

    /// <summary>Replaces the source items that map to one data row; a listed item that maps to another row moves to this one.</summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="versionId">A draft or published report version.</param>
    /// <param name="input">The row and its complete item list.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The design afterwards.</returns>
    /// <response code="200">The design afterwards.</response>
    /// <response code="400">The input breaks a field rule, such as a group row or an item the bound dimension lacks; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is neither a draft nor published.</response>
    [HttpPut("row-mappings")]
    [RequireEditable(VersionKinds.ReportVersion, "versionId")]
    [ProducesResponseType<ReportDesign>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<ReportDesign>> ReplaceRowMappings(string tenantId, long versionId, RowMappingsInput input, CancellationToken cancellationToken)
    {
        return Ok(await rowMappings.ReplaceAsync(versionId, input, cancellationToken));
    }

    /// <summary>
    /// Moves the design to another status: the report dimension's version, each bridge's version and the report version change
    /// together. Leaving draft runs every version's publication rules with the set's versions counted as published, so the set
    /// validates as a whole; publishing writes the dimension first and the report last.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="versionId">The report version.</param>
    /// <param name="input">The target status.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The report version's new state; 409 when the lifecycle forbids the transition; 422 with the violations when rules reject it.</returns>
    /// <response code="200">The transition applied; the body is the report version's new state.</response>
    /// <response code="400">The target status is not a known value.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The lifecycle does not allow moving from the current status to the target; the detail names both.</response>
    /// <response code="422">Publication rules rejected the transition and no status changed; <c>violations</c> lists why.</response>
    [HttpPut("status")]
    [ProducesResponseType<VersionState>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<RuleViolationProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<VersionState>> ChangeStatus(string tenantId, long versionId, StatusChangeInput input, CancellationToken cancellationToken)
    {
        var result = await workflow.TransitionAsync(await designs.SetAsync(versionId, cancellationToken), input.Status, cancellationToken);
        var report = result.States[^1];
        if (result.Outcome == TransitionOutcome.NotAllowed)
        {
            var detail = $"The design of report version {versionId} is {report.Status}; it cannot move to {input.Status}.";
            return Problem(detail, statusCode: StatusCodes.Status409Conflict, title: "Transition not allowed");
        }

        if (result.Outcome == TransitionOutcome.Rejected)
            return RuleViolationResults.Create(HttpContext, $"The design of report version {versionId} cannot become {input.Status}.", result.Violations);
        return Ok(report);
    }

    /// <summary>
    /// Checks the design against the publication rules of its versions, counted as published together, and changes nothing. Validate
    /// on the published design runs this check, because its edits are live.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the report.</param>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The violations the rules found.</returns>
    /// <response code="200">The check ran; <c>violations</c> lists what the rules found and is empty when the design passes.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpPost("validation")]
    [ProducesResponseType<VersionCheck>(StatusCodes.Status200OK)]
    public async Task<ActionResult<VersionCheck>> Check(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(new VersionCheck { Violations = await workflow.CheckAsync(await designs.SetAsync(versionId, cancellationToken), cancellationToken) });
    }
}
