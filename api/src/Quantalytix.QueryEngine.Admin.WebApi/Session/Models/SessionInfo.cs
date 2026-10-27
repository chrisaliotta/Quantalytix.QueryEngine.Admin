namespace Quantalytix.QueryEngine.Admin.WebApi.Session;

/// <summary>The contract's <c>Session</c>: who the caller is, as the API sees its bearer token. There are no tenant roles (OI-09).</summary>
public sealed record SessionInfo
{
    /// <summary>The token's subject.</summary>
    public required string Subject { get; init; }

    /// <summary>The token's name claim; null when it carries none.</summary>
    public required string? DisplayName { get; init; }

    /// <summary>The caller's tenant when the token carries exactly one non-blank tenant claim; otherwise null.</summary>
    public required string? TenantId { get; init; }

    /// <summary>Whether the caller may reach every tenant and the tenant directory.</summary>
    public required bool IsSystemAdmin { get; init; }
}
