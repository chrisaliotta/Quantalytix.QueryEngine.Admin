namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>The authenticated caller, as its validated bearer token describes it.</summary>
public sealed record Caller
{
    /// <summary>The token's subject.</summary>
    public required string Subject { get; init; }

    /// <summary>The caller's tenant when the token carries exactly one non-blank tenant claim; otherwise null.</summary>
    public string? TenantId { get; init; }

    /// <summary>Whether the token carries the SystemAdmin role, which admits every tenant.</summary>
    public required bool IsSystemAdmin { get; init; }
}
