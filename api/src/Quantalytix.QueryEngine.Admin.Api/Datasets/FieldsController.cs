using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Authorization;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Api.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Datasets;

/// <summary>Edits the fields of dataset versions, creates fields from physical columns and sets their materialized columns.</summary>
[ApiController]
[Route("api/tenants/{tenantId}/dataset-versions/{versionId:long}/fields")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class FieldsController(
    FieldService fields,
    FieldsFromColumnsService fieldsFromColumns,
    MaterializedColumnService materializedColumns) : ControllerBase
{
    /// <summary>Lists the fields of a dataset version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The dataset version.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The fields, sorted by key.</returns>
    /// <response code="200">The fields, sorted by key.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<FieldRecord>>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<IReadOnlyList<FieldRecord>>> List(string tenantId, long versionId, CancellationToken cancellationToken)
    {
        return Ok(await fields.ListAsync(versionId, cancellationToken));
    }

    /// <summary>Creates a field in a draft dataset version.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft dataset version.</param>
    /// <param name="input">The new field.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The new field.</returns>
    /// <response code="200">The new field.</response>
    /// <response code="400">The input breaks a field rule of its source kind or data type; errors are keyed by camelCase JSON path.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPost]
    [RequireDraft(VersionKinds.DatasetVersion, "versionId")]
    [ProducesResponseType<FieldRecord>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<FieldRecord>> Create(string tenantId, long versionId, FieldInput input, CancellationToken cancellationToken)
    {
        return Ok(await fields.CreateAsync(versionId, input, cancellationToken));
    }

    /// <summary>
    /// Replaces a field's editable attributes; the key is fixed. Changing how the value is computed clears the materialized column.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="reference">The draft dataset version and its field.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The updated field.</returns>
    /// <response code="200">The updated field.</response>
    /// <response code="400">The field is not in the version, or the input breaks a field rule.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPut("{fieldId:long}")]
    [RequireDraft(VersionKinds.DatasetVersion, "versionId")]
    [ProducesResponseType<FieldRecord>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<FieldRecord>> Update(
        string tenantId,
        [FromRoute] FieldReference reference,
        FieldUpdateInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await fields.UpdateAsync(reference, input, cancellationToken));
    }

    /// <summary>Deletes a field that no grain, binding, time binding or measure component references.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="reference">The draft dataset version and its field.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when other rows reference the field.</returns>
    /// <response code="204">The field is deleted.</response>
    /// <response code="400">The field is not in the version.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft, or a grain, binding, time binding or measure component references the field; the detail names them.</response>
    [HttpDelete("{fieldId:long}")]
    [RequireDraft(VersionKinds.DatasetVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Delete(string tenantId, [FromRoute] FieldReference reference, CancellationToken cancellationToken)
    {
        var conflict = await fields.DeleteAsync(reference, cancellationToken);
        if (conflict is not null) return Problem(conflict, statusCode: StatusCodes.Status409Conflict, title: DeleteBlockedProblem.Title);
        return NoContent();
    }

    /// <summary>
    /// Creates one <c>column</c> field per selected physical column of the version's source object, named and typed from the
    /// column. Columns without a logical type, or already read by a <c>column</c> field of the version, are skipped.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="versionId">The draft dataset version.</param>
    /// <param name="input">The selected column names.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The new fields, in column order.</returns>
    /// <response code="200">The new fields, in column order; empty when every selected column was skipped.</response>
    /// <response code="400">No column is selected.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft.</response>
    [HttpPost("from-columns")]
    [RequireDraft(VersionKinds.DatasetVersion, "versionId")]
    [ProducesResponseType<IReadOnlyList<FieldRecord>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<FieldRecord>>> CreateFromColumns(
        string tenantId,
        long versionId,
        FieldsFromColumnsInput input,
        CancellationToken cancellationToken)
    {
        return Ok(await fieldsFromColumns.CreateAsync(versionId, input, cancellationToken));
    }

    /// <summary>
    /// Adds a generated column holding a <c>json_path</c> field's value to the version's source table, waits for the
    /// asynchronous schema change to finish, then points the draft field at the column.
    /// </summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="reference">The draft dataset version and its field.</param>
    /// <param name="input">The new column's name.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when the field or its source cannot be materialized.</returns>
    /// <response code="204">The column exists and the field's materialized column names it.</response>
    /// <response code="400">The field is not in the version, or the column name breaks the identifier pattern.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft, the field is not a json_path field, the source object is a view, or the table already has the column.</response>
    [HttpPost("{fieldId:long}/materialize")]
    [RequireDraft(VersionKinds.DatasetVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Materialize(
        string tenantId,
        [FromRoute] FieldReference reference,
        MaterializeInput input,
        CancellationToken cancellationToken)
    {
        var refusal = await materializedColumns.MaterializeAsync(reference, input, cancellationToken);
        if (refusal is not null) return Problem(refusal, statusCode: StatusCodes.Status409Conflict, title: "Field cannot be materialized");
        return NoContent();
    }

    /// <summary>Points a <c>json_path</c> or <c>expression</c> field at an existing column of the version's source object, or clears it.</summary>
    /// <param name="tenantId">The tenant that owns the version.</param>
    /// <param name="reference">The draft dataset version and its field.</param>
    /// <param name="input">The column, or null to clear it.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>No content; 409 when the field reads a column directly or the column does not exist.</returns>
    /// <response code="204">The field's materialized column is set or cleared.</response>
    /// <response code="400">The field is not in the version, or the column name breaks the identifier pattern.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    /// <response code="403">The caller's token names another tenant and lacks the SystemAdmin role.</response>
    /// <response code="409">The version is not a draft, the field is a column field, or the source object has no such column.</response>
    [HttpPut("{fieldId:long}/materialized-column")]
    [RequireDraft(VersionKinds.DatasetVersion, "versionId")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> SetMaterializedColumn(
        string tenantId,
        [FromRoute] FieldReference reference,
        MaterializedColumnInput input,
        CancellationToken cancellationToken)
    {
        var refusal = await materializedColumns.SetAsync(reference, input, cancellationToken);
        if (refusal is not null) return Problem(refusal, statusCode: StatusCodes.Status409Conflict, title: "Materialized column refused");
        return NoContent();
    }
}
