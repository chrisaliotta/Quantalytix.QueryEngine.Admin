namespace Quantalytix.QueryEngine.Admin.WebApi.Errors;

/// <summary>The stable <see cref="Problem.Type"/> codes a client may branch on.</summary>
public static class ProblemTypes
{
    /// <summary>The body, route or query does not bind, or an input validator failed (400).</summary>
    public const string InvalidInput = Prefix + "invalid-input";

    /// <summary>No valid bearer token (401).</summary>
    public const string Unauthorized = Prefix + "unauthorized";

    /// <summary>No access to the route tenant, or <c>SystemAdmin</c> missing (403).</summary>
    public const string Forbidden = Prefix + "forbidden";

    /// <summary>An id in the route names no row in the tenant, or no route matches (404).</summary>
    public const string NotFound = Prefix + "not-found";

    /// <summary>A precondition of the action failed (409); the detail says which.</summary>
    public const string Conflict = Prefix + "conflict";

    /// <summary>Other rows still reference what a delete would remove (409).</summary>
    public const string DeleteBlocked = Prefix + "delete-blocked";

    /// <summary>The change would break a rule; nothing was written (422).</summary>
    public const string RulesViolated = Prefix + "rules-violated";

    /// <summary>An unhandled error (500).</summary>
    public const string Internal = Prefix + "internal";

    /// <summary>The URN prefix of every code.</summary>
    private const string Prefix = "urn:qe:problem:";
}
