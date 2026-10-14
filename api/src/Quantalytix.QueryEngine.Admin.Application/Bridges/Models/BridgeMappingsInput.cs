namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The complete mapping list of a draft bridge version, as <c>PUT bridge-versions/{versionId}/mappings</c> takes it.</summary>
public sealed record BridgeMappingsInput
{
    /// <summary>The mappings; each source and target pair appears once.</summary>
    public required IReadOnlyList<BridgeMappingInput> Mappings { get; init; }
}
