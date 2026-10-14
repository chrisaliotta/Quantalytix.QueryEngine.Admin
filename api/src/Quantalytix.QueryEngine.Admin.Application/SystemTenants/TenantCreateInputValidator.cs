using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.SystemTenants;

/// <summary>
/// Checks a new tenant directory row: a well-formed, unused tenant ID, a known provider, a cache lifetime of zero or more,
/// and a connection string that routes to the tenant's own metadata database.
/// </summary>
public sealed class TenantCreateInputValidator : AbstractValidator<TenantCreateInput>
{
    /// <summary>A letter or digit, then letters, digits, <c>_</c> and <c>-</c>.</summary>
    private const string TenantIdPattern = "^[A-Za-z0-9][A-Za-z0-9_-]*$";

    /// <summary>Declares the rules.</summary>
    /// <param name="tenants">The tenant directory, read for the uniqueness check.</param>
    /// <param name="verification">Verifies the connection string against the new tenant ID.</param>
    public TenantCreateInputValidator(ITableGateway<TenantDirectoryRecord> tenants, TenantConnectionVerification verification)
    {
        RuleFor(input => input.TenantId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(128)
            .Matches(TenantIdPattern)
            .WithMessage("'tenantId' must start with a letter or digit and contain only letters, digits, '_' and '-'.")
            .MustAsync(async (tenantId, cancellationToken) => (await tenants.ListAsync(new { TenantId = tenantId }, cancellationToken)).Count == 0)
            .WithMessage(input => $"Tenant '{input.TenantId}' already exists.");
        RuleFor(input => input.ProviderId).Cascade(CascadeMode.Stop).NotEmpty().MustBeIn(ProviderIds.Definition);
        RuleFor(input => input.MetadataCacheSeconds).GreaterThanOrEqualTo(0);
        RuleFor(input => input.ConnectionString)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustFitVarchar(4096)
            .MustRouteToTenant(verification, input => input.TenantId);
    }
}
