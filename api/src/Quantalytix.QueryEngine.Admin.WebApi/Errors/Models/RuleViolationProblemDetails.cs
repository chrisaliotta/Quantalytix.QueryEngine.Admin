using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Api.Errors;

/// <summary>A 422 problem whose <c>violations</c> list every rule that rejected the change.</summary>
public sealed class RuleViolationProblemDetails : ProblemDetails
{
    /// <summary>The rule violations, each with a snake_case rule code and a message.</summary>
    public required IReadOnlyList<RuleViolation> Violations { get; init; }
}
