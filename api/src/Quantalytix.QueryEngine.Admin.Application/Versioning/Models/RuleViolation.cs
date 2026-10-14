namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>One reason a publication rule rejects a version.</summary>
public sealed record RuleViolation
{
    /// <summary>A short snake_case code for the rule, such as <c>closure_mismatch</c>.</summary>
    public required string Rule { get; init; }

    /// <summary>What is wrong, written for the person editing the version.</summary>
    public required string Message { get; init; }
}
