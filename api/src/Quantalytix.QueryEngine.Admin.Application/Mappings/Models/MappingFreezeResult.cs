using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>The contract's <c>MappingFreezeResult</c>: the passing rule run on version n and the new live version n+1.</summary>
public sealed record MappingFreezeResult
{
    /// <summary>The rule run on the frozen version, which found nothing.</summary>
    public required ValidationReport Report { get; init; }

    /// <summary>The new live version.</summary>
    public required MappingVersionSummary Live { get; init; }
}
