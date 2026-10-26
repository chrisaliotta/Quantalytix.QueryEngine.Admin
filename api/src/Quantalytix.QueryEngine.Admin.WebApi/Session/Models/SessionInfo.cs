namespace Quantalytix.QueryEngine.Admin.WebApi.Session;

/// <summary>Who the caller is, as the API sees its bearer token.</summary>
public sealed record SessionInfo
{
    /// <summary>The token's subject.</summary>
    public required string Subject { get; init; }

    /// <summary>The caller's tenant when the token carries exactly one non-blank tenant claim; otherwise null.</summary>
    public string? TenantId { get; init; }

    /// <summary>Whether the caller is a SystemAdmin, who may administer every tenant.</summary>
    public required bool IsSystemAdmin { get; init; }
}
