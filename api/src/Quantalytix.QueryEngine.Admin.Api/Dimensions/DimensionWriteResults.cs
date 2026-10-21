using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Api.Errors;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Api.Dimensions;

/// <summary>Builds the 422 answers of member writes that the live objects of a published dimension version refuse.</summary>
internal static class DimensionWriteResults
{
    /// <summary>The answer to a write that would leave a key a live object needs unresolved; nothing was written.</summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="dimensionVersionId">The dimension version.</param>
    /// <param name="violations">The keys the write would leave unresolved.</param>
    /// <returns>The 422 result.</returns>
    public static ObjectResult Rejected(HttpContext httpContext, long dimensionVersionId, IReadOnlyList<RuleViolation> violations)
    {
        return RuleViolationResults.Create(
            httpContext,
            $"The change would leave keys that live objects name in dimension version {dimensionVersionId} unresolved; nothing was written.",
            violations);
    }
}
