namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>A successful freeze: version n passed its rules and is frozen, and n+1 is the identity's new live version.</summary>
public sealed record FrozenVersion
{
    /// <summary>The rule run on version n, which found nothing.</summary>
    public required ValidationReport Report { get; init; }

    /// <summary>The new live version n+1; the module answers its version summary as the contract's <c>live</c>.</summary>
    public required long LiveVersionId { get; init; }
}
