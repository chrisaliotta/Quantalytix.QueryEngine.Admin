using System.Text.Json.Serialization;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.WebApi.Errors;

/// <summary>
/// The one error body of the API (<c>application/problem+json</c>, blueprint 01, OI-06): 400 carries <c>errors</c>, 409
/// carries <c>violations</c> for delete blockers and <c>detail</c> otherwise, 422 carries <c>violations</c>, 401, 403 and
/// 404 carry the title only, and 500 carries <c>traceId</c>. Built by <c>Problems</c>.
/// </summary>
public sealed record Problem
{
    /// <summary>The stable problem code, such as <c>urn:qe:problem:rules-violated</c>.</summary>
    public required string Type { get; init; }

    /// <summary>A short, user-facing summary.</summary>
    public required string Title { get; init; }

    /// <summary>The HTTP status code.</summary>
    public required int Status { get; init; }

    /// <summary>What happened, when the title does not say it all; omitted otherwise.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; init; }

    /// <summary>Input path to messages; <c>''</c> is the form-level key. Empty unless the status is 400.</summary>
    public required IReadOnlyDictionary<string, string[]> Errors { get; init; }

    /// <summary>The rule violations (422) or delete blockers (409); omitted otherwise.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RuleViolation>? Violations { get; init; }

    /// <summary>The request's trace id on a 500; omitted otherwise.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; init; }
}
