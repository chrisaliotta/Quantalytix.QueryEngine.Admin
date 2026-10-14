using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.SystemTenants;

/// <summary>
/// Checks an edit of a tenant directory row: a known provider, a cache lifetime of zero or more, and, when a new
/// connection string is supplied, that it routes to the tenant's own metadata database. The edited tenant is the route's
/// <c>{tenantId}</c>, which <see cref="ITenantContext"/> names.
/// </summary>
public sealed class TenantUpdateInputValidator : AbstractValidator<TenantUpdateInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="tenant">Names the edited tenant from the route.</param>
    /// <param name="verification">Verifies a new connection string against the edited tenant.</param>
    public TenantUpdateInputValidator(ITenantContext tenant, TenantConnectionVerification verification)
    {
        RuleFor(input => input.ProviderId).Cascade(CascadeMode.Stop).NotEmpty().MustBeIn(ProviderIds.Definition);
        RuleFor(input => input.MetadataCacheSeconds).GreaterThanOrEqualTo(0);
        RuleFor(input => input.ConnectionString)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(4096)
            .MustRouteToTenant(verification, _ => tenant.TenantId)
            .When(input => input.ConnectionString is not null);
    }
}
