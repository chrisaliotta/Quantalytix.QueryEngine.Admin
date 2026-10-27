using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// One reason a rule rejects a version, a report, a write or a delete: the contract's <c>Violation</c>, and with a
/// <c>DeleteBlockerCode</c> as its code, a <c>DeleteBlocker</c>.
/// </summary>
public sealed record RuleViolation
{
    /// <summary>A short snake_case code for the rule, such as <c>closure_mismatch</c>.</summary>
    public required string Code { get; init; }

    /// <summary>What is wrong, written for the person editing the version or the report.</summary>
    public required string Message { get; init; }

    /// <summary>Where to take the user to fix it; omitted when the rule names no place.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ViolationTarget? Target { get; init; }
}
