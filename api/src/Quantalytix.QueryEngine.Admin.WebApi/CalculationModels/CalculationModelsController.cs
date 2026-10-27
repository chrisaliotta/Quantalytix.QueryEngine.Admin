using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.CalculationModels;

/// <summary>
/// The model manager (blueprint 15): the list, create, the model document, the formula check, the delete check and delete, and the
/// model change set, whose preview refuses an edit that introduces a model-rule or enabled-report violation (OI-42).
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}/calculation-models")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class CalculationModelsController(
    ModelQueries queries,
    ModelCommands commands,
    ChangeSetPipeline<ModelSnapshot, ModelOperation, ModelDocument> changes) : ControllerBase
{
    /// <summary>Lists the tenant's models with their metric counts and the reports that read them.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The list items by key.</returns>
    /// <response code="200">Every model of the tenant.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ModelListItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status403Forbidden, ProblemWriter.ContentType)]
    public async Task<ActionResult<IReadOnlyList<ModelListItem>>> List(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await queries.ListAsync(cancellationToken));
    }

    /// <summary>Creates a disabled model without metrics.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="input">The new model.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The model's settings.</returns>
    /// <response code="201">Created.</response>
    /// <response code="400">The input breaks a field rule.</response>
    /// <response code="409">The model key is taken.</response>
    [HttpPost]
    [ProducesResponseType<CalcModel>(StatusCodes.Status201Created)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<ActionResult<CalcModel>> Create(string tenantId, CalcModelCreateInput input, CancellationToken cancellationToken)
    {
        var outcome = await commands.CreateAsync(input, cancellationToken);
        return outcome.Succeeded ? StatusCode(StatusCodes.Status201Created, outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }

    /// <summary>Reads a model with its variables, measures, parameters, attributions, effective drivers and readers.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="modelId">The model.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The document.</returns>
    /// <response code="200">The model document.</response>
    [HttpGet("{modelId:long}")]
    [ProducesResponseType<ModelDocument>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModelDocument>> Get(string tenantId, long modelId, CancellationToken cancellationToken)
    {
        return Ok(await queries.DocumentAsync(modelId, cancellationToken));
    }

    /// <summary>Deletes a model that no report reads, with its content (C8).</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="modelId">The model.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Deleted.</response>
    /// <response code="400">The route does not bind.</response>
    /// <response code="409">A report reads the model; <c>violations</c> carry <c>referenced_by_report</c> per report.</response>
    [HttpDelete("{modelId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status409Conflict, ProblemWriter.ContentType)]
    public async Task<IActionResult> Delete(string tenantId, long modelId, CancellationToken cancellationToken)
    {
        return await commands.DeleteAsync(modelId, cancellationToken) is { } refusal ? Problems.From(refusal).ToResult() : NoContent();
    }

    /// <summary>Checks whether a model can be deleted.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="modelId">The model.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The delete check.</returns>
    /// <response code="200">The delete check.</response>
    [HttpGet("{modelId:long}/delete-check")]
    [ProducesResponseType<DeleteCheck>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeleteCheck>> CheckDelete(string tenantId, long modelId, CancellationToken cancellationToken)
    {
        return Ok(await queries.DeleteCheckAsync(modelId, cancellationToken));
    }

    /// <summary>Checks a formula against the saved model: errors with their positions, reads, parameters and the language it needs (OI-43).</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="modelId">The model.</param>
    /// <param name="request">The formula and the metric that owns it.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The check.</returns>
    /// <response code="200">The check result; never writes.</response>
    /// <response code="400">The request breaks a field rule.</response>
    [HttpPost("{modelId:long}/formula-check")]
    [ProducesResponseType<ModelFormulaCheck>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    public async Task<ActionResult<ModelFormulaCheck>> CheckFormula(string tenantId, long modelId, ModelFormulaCheckRequest request, CancellationToken cancellationToken)
    {
        return Ok(await queries.CheckFormulaAsync(modelId, request, cancellationToken));
    }

    /// <summary>Previews a model change set: applies the operations to the stored model in memory, runs the model gate, and writes nothing.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="modelId">The model.</param>
    /// <param name="request">The operations so far.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The pending document and every violation it has.</returns>
    /// <response code="200">The operations apply.</response>
    /// <response code="400">An operation's input is invalid or names an item the model lacks; errors are keyed by <c>operations[n]</c>.</response>
    /// <response code="422">The operations introduce a model-rule or enabled-report violation.</response>
    [HttpPost("{modelId:long}/changes/preview")]
    [ProducesResponseType<ChangeSetPreview<ModelDocument>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<ChangeSetPreview<ModelDocument>>> Preview(
        string tenantId,
        long modelId,
        ChangeSetRequest<ModelOperation> request,
        CancellationToken cancellationToken)
    {
        return OkOrProblem(await changes.PreviewAsync(modelId, request, cancellationToken));
    }

    /// <summary>Commits a model change set: re-runs the model gate on the model as stored now and writes the operations as one batch.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="modelId">The model.</param>
    /// <param name="request">Every operation of the change set.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The stored document.</returns>
    /// <response code="200">Every operation applied and was written.</response>
    /// <response code="400">An operation's input is invalid or names an item the model lacks; errors are keyed by <c>operations[n]</c>.</response>
    /// <response code="422">The operations introduce a model-rule or enabled-report violation; nothing was written.</response>
    [HttpPost("{modelId:long}/changes")]
    [ProducesResponseType<ChangeSetCommit<ModelDocument>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Problem>(StatusCodes.Status400BadRequest, ProblemWriter.ContentType)]
    [ProducesResponseType<Problem>(StatusCodes.Status422UnprocessableEntity, ProblemWriter.ContentType)]
    public async Task<ActionResult<ChangeSetCommit<ModelDocument>>> Commit(
        string tenantId,
        long modelId,
        ChangeSetRequest<ModelOperation> request,
        CancellationToken cancellationToken)
    {
        return OkOrProblem(await changes.CommitAsync(modelId, request, cancellationToken));
    }

    /// <summary>200 with the value, or the refusal's problem.</summary>
    private ActionResult<T> OkOrProblem<T>(Outcome<T> outcome) where T : class
    {
        return outcome.Succeeded ? Ok(outcome.Value) : Problems.From(outcome.Refusal).ToResult();
    }
}
