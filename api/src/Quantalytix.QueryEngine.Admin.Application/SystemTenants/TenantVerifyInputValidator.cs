using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.SystemTenants;

/// <summary>Checks that a connection test names a tenant and a connection string; the test's outcome is its response, not a validation error.</summary>
public sealed class TenantVerifyInputValidator : AbstractValidator<TenantVerifyInput>
{
    /// <summary>Declares the rules.</summary>
    public TenantVerifyInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty().MaximumLength(128);
        RuleFor(input => input.ConnectionString).NotEmpty().MaximumLength(4096);
    }
}
