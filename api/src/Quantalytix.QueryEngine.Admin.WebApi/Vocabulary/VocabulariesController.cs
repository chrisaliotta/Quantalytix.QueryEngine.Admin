using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;

namespace Quantalytix.QueryEngine.Admin.WebApi.Vocabulary;

/// <summary>Serves the controlled vocabularies every select list renders.</summary>
[ApiController]
[Route("api/vocabularies")]
[ProducesResponseType<Problem>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class VocabulariesController : ControllerBase
{
    /// <summary>Read every enumeration the UI renders.</summary>
    /// <returns>Every vocabulary, keyed by name.</returns>
    /// <response code="200">Every vocabulary, keyed by name.</response>
    /// <response code="401">No valid bearer token.</response>
    [HttpGet(Name = "getVocabularies")]
    [ProducesResponseType<Vocabularies>(StatusCodes.Status200OK)]
    public ActionResult<Vocabularies> Get()
    {
        return Ok(Vocabularies.FromCatalog());
    }
}
