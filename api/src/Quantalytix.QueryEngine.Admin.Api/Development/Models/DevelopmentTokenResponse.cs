namespace Quantalytix.QueryEngine.Admin.Api.Development;

/// <summary>A bearer token issued by a Development host.</summary>
public sealed record DevelopmentTokenResponse
{
    /// <summary>The signed JWT to send as <c>Authorization: Bearer ...</c>.</summary>
    public required string AccessToken { get; init; }

    /// <summary>The tenant claim the token carries.</summary>
    public required string TenantId { get; init; }

    /// <summary>Whether the token carries the SystemAdmin role.</summary>
    public required bool SystemAdmin { get; init; }

    /// <summary>When the token expires.</summary>
    public required DateTimeOffset ExpiresUtc { get; init; }
}
