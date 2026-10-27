using Microsoft.AspNetCore.Mvc;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.WebApi.Errors;

/// <summary>Creates every <see cref="Problem"/> the API answers with, so each status has one shape and one code.</summary>
public static class Problems
{
    /// <summary>The 400 for input that does not bind or fails a validator.</summary>
    /// <param name="errors">Input path to messages, camelCase and indexed, such as <c>components[0].fieldId</c>.</param>
    /// <returns>The problem.</returns>
    public static Problem Invalid(IReadOnlyDictionary<string, string[]> errors)
    {
        return Create(StatusCodes.Status400BadRequest, ProblemTypes.InvalidInput, "The request is not valid.") with { Errors = errors };
    }

    /// <summary>The 409 for a failed precondition, such as a version that is not a draft.</summary>
    /// <param name="title">The short summary, such as <c>Version is not a draft</c>.</param>
    /// <param name="detail">What failed.</param>
    /// <returns>The problem.</returns>
    public static Problem Conflict(string title, string detail)
    {
        return Create(StatusCodes.Status409Conflict, ProblemTypes.Conflict, title) with { Detail = detail };
    }

    /// <summary>The 409 for a blocked delete, listing the blockers as violations.</summary>
    /// <param name="check">A check that does not allow the delete.</param>
    /// <returns>The problem.</returns>
    public static Problem DeleteBlocked(DeleteCheck check)
    {
        return Create(StatusCodes.Status409Conflict, ProblemTypes.DeleteBlocked, "Delete blocked") with
        {
            Detail = check.Message,
            Violations = check.Reasons
        };
    }

    /// <summary>
    /// The 409 for a blocked delete of a resource whose service still answers with a sentence rather than a
    /// <see cref="DeleteCheck"/>; it carries the sentence only until the module answers with the check.
    /// </summary>
    /// <param name="detail">The sentence naming what blocks the delete.</param>
    /// <returns>The problem.</returns>
    public static Problem DeleteBlocked(string detail)
    {
        return Create(StatusCodes.Status409Conflict, ProblemTypes.DeleteBlocked, "Delete blocked") with { Detail = detail };
    }

    /// <summary>The 422 for a change that would break rules; nothing was written.</summary>
    /// <param name="detail">What was refused, such as <c>Report 42 is enabled, and the change would break its rules.</c></param>
    /// <param name="violations">The rules the change breaks.</param>
    /// <returns>The problem.</returns>
    public static Problem RulesViolated(string? detail, IReadOnlyList<RuleViolation> violations)
    {
        return Create(StatusCodes.Status422UnprocessableEntity, ProblemTypes.RulesViolated, "The change breaks metadata rules") with
        {
            Detail = detail,
            Violations = violations
        };
    }

    /// <summary>
    /// The problem of an Application refusal: 400 with its errors, 409 with its detail or its delete blockers, or 422 with its
    /// violations.
    /// </summary>
    /// <param name="refusal">Why a change set or command wrote nothing.</param>
    /// <returns>The problem.</returns>
    public static Problem From(Refusal refusal)
    {
        return refusal.Kind switch
        {
            RefusalKind.Invalid => Invalid(refusal.Errors),
            RefusalKind.Conflict => Conflict(refusal.Title!, refusal.Detail!),
            RefusalKind.DeleteBlocked => DeleteBlocked(new DeleteCheck { Allowed = false, Message = refusal.Detail, Reasons = refusal.Violations }),
            _ => RulesViolated(refusal.Detail, refusal.Violations)
        };
    }

    /// <summary>The 500 for an unhandled error.</summary>
    /// <param name="traceId">The request's trace id, which the logs carry too.</param>
    /// <returns>The problem.</returns>
    public static Problem Internal(string traceId)
    {
        return Create(StatusCodes.Status500InternalServerError, ProblemTypes.Internal, "An unexpected error occurred.") with { TraceId = traceId };
    }

    /// <summary>The title-only problem of a status that left without a body: 401, 403, 404, or any other client error.</summary>
    /// <param name="status">The response's status code.</param>
    /// <returns>The problem.</returns>
    public static Problem OfStatus(int status)
    {
        return status switch
        {
            StatusCodes.Status401Unauthorized => Create(status, ProblemTypes.Unauthorized, "Authentication is required."),
            StatusCodes.Status403Forbidden => Create(status, ProblemTypes.Forbidden, "Access to this resource is not allowed."),
            StatusCodes.Status404NotFound => Create(status, ProblemTypes.NotFound, "Not found."),
            _ => Create(status, ProblemTypes.InvalidInput, "The request is not valid.")
        };
    }

    /// <summary>An MVC result that writes the problem as <c>application/problem+json</c> with its status.</summary>
    /// <param name="problem">The problem.</param>
    /// <returns>The result.</returns>
    public static ObjectResult ToResult(this Problem problem)
    {
        return new ObjectResult(problem) { StatusCode = problem.Status, ContentTypes = { ProblemWriter.ContentType } };
    }

    /// <summary>A problem with no errors, detail, violations or trace id.</summary>
    private static Problem Create(int status, string type, string title)
    {
        return new Problem { Type = type, Title = title, Status = status, Errors = new Dictionary<string, string[]>() };
    }
}
