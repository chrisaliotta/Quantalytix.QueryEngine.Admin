using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>The contract's <c>BridgeFreezeResult</c>: the passing rule run on version n and the new live version n+1.</summary>
public sealed record BridgeFreezeResult
{
    /// <summary>The rule run on the frozen version, which found nothing.</summary>
    public required ValidationReport Report { get; init; }

    /// <summary>The new live version, with the same settings and mappings.</summary>
    public required BridgeVersionSummary Live { get; init; }
}
