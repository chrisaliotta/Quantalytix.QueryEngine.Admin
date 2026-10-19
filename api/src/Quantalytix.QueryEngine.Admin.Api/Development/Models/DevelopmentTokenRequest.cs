namespace Quantalytix.QueryEngine.Admin.Api.Development;

/// <summary>The tenant a development token names, and whether it carries the SystemAdmin role.</summary>
public sealed record DevelopmentTokenRequest
{
    /// <summary>The tenant claim the token carries, such as <c>development</c>.</summary>
    public required string TenantId { get; init; }

    /// <summary>Whether the token carries the SystemAdmin role, which admits every tenant. Defaults to false.</summary>
    public bool SystemAdmin { get; init; }
}
