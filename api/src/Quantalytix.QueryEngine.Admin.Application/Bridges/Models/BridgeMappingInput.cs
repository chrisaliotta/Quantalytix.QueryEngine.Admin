using System.Text.Json.Serialization;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The contract's <c>BridgeMappingInput</c>: one mapping of the live version, named by its id.</summary>
public sealed record BridgeMappingInput
{
    /// <summary>The mapping's stored id, or a client-generated <c>tmp:&lt;uuid&gt;</c> for a new mapping.</summary>
    public required string Id { get; init; }

    /// <summary>The source member's key.</summary>
    public required string SourceMemberKey { get; init; }

    /// <summary>The target member's key.</summary>
    public required string TargetMemberKey { get; init; }

    /// <summary>The coefficient measures with <c>bridge_factor_behavior = value</c> apply (B7).</summary>
    public required decimal Factor { get; init; }

    /// <summary>The share of the source's value the target takes.</summary>
    public required decimal AllocationWeight { get; init; }

    /// <summary>Report bridges only, written by the report editor; absent means <c>member</c> (OI-28).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceSelection { get; init; }
}
