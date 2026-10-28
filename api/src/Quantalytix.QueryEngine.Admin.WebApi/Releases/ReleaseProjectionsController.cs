using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.WebApi.Authorization;

namespace Quantalytix.QueryEngine.Admin.WebApi.Releases;

/// <summary>
/// The projections the release and assumption set screens render (blueprint 14, OI-13): the datasets a manifest can pin, the identities
/// a set pins, and what each report reads.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId}")]
[Authorize(Policy = AdminPolicies.TenantAccess)]
public sealed class ReleaseProjectionsController(ManifestDatasetReader manifestDatasets, AssumptionSetQueries sets, ReleaseQueries releases) : ControllerBase
{
    /// <summary>Lists the datasets a manifest can pin, each with every version, its bindings and its measures.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The datasets.</returns>
    /// <response code="200">Every dataset by key, its versions newest first.</response>
    [HttpGet("manifest-datasets")]
    [ProducesResponseType<IReadOnlyList<ManifestDataset>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ManifestDataset>>> ManifestDatasets(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await manifestDatasets.ListAsync(cancellationToken));
    }

    /// <summary>Lists the dimensions, mapping sets and bridges an assumption set pins, with their live and frozen versions.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The identities.</returns>
    /// <response code="200">Every identity, report-owned ones included.</response>
    [HttpGet("identities")]
    [ProducesResponseType<IReadOnlyList<PinIdentity>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PinIdentity>>> Identities(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await sets.IdentitiesAsync(cancellationToken));
    }

    /// <summary>Lists what each report reads from releases (its sources) and assumption sets (its identities).</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <returns>The report references.</returns>
    /// <response code="200">Every report by key, its sources' datasets resolved in the active release.</response>
    [HttpGet("report-reads")]
    [ProducesResponseType<IReadOnlyList<ReleaseReportRef>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReleaseReportRef>>> ReportReads(string tenantId, CancellationToken cancellationToken)
    {
        return Ok(await releases.ReportReadsAsync(cancellationToken));
    }
}
