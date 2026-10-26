using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using VocabularyDefinition = Quantalytix.QueryEngine.Admin.Application.Vocabulary.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Api.Vocabulary;

/// <summary>Serves the controlled vocabularies every select list and validator uses.</summary>
[ApiController]
[Route("api/vocabularies")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class VocabulariesController : ControllerBase
{
    /// <summary>Lists every vocabulary with its allowed values and their definitions.</summary>
    /// <returns>Every vocabulary.</returns>
    /// <response code="200">Every vocabulary, each with its values in the schema's order.</response>
    /// <response code="401">The request has no valid bearer token.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<VocabularyDefinition>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<VocabularyDefinition>> List()
    {
        return Ok(VocabularyCatalog.All);
    }
}
