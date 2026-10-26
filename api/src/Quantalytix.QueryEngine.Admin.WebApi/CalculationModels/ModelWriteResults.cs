using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.WebApi.Errors;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.WebApi.CalculationModels;

/// <summary>Builds the 422 answers of model writes that the model's rules refuse.</summary>
internal static class ModelWriteResults
{
    /// <summary>The answer to a write that would break the model's formulas or attributions, or an enabled report that reads it; nothing was written.</summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="calculationModelId">The calculation model.</param>
    /// <param name="violations">What the write would break.</param>
    /// <returns>The 422 result.</returns>
    public static ObjectResult Rejected(HttpContext httpContext, long calculationModelId, IReadOnlyList<RuleViolation> violations)
    {
        return RuleViolationResults.Create(
            httpContext,
            $"The change would break calculation model {calculationModelId}'s formulas or attributions, or an enabled report that reads it; nothing was written.",
            violations);
    }
}
