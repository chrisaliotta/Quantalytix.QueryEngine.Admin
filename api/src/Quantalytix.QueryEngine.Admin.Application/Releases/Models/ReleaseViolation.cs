using System.Text.Json.Serialization;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// The contract's <c>ReleaseViolation</c>: a release rule finding with the manifest row it belongs to, for inline issue icons. Its
/// target is a dataset (<c>itemId</c> the dataset), an identity (<c>area</c> its pin kind) or a report (<c>itemId</c> the report).
/// </summary>
public sealed record ReleaseViolation
{
    /// <summary>A <c>ReleaseRuleCode</c>.</summary>
    public required string Code { get; init; }

    /// <summary>What is wrong.</summary>
    public required string Message { get; init; }

    /// <summary>The manifest row's dataset; null when the finding belongs to no row.</summary>
    public required long? DatasetId { get; init; }

    /// <summary>Where to fix it; omitted when the finding names no place.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ViolationTarget? Target { get; init; }

    /// <summary>The finding as the kernel's violation, which problems and the version workflow carry.</summary>
    /// <returns>The violation without its manifest row.</returns>
    public RuleViolation ToRuleViolation()
    {
        return new RuleViolation { Code = Code, Message = Message, Target = Target };
    }
}
