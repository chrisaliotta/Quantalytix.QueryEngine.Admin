using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.SystemTenants;

/// <summary>FluentValidation rules for tenant connection strings.</summary>
public static class TenantConnectionRuleExtensions
{
    /// <summary>
    /// Requires the connection string to route to the tenant's own metadata database, as
    /// <see cref="TenantConnectionVerification"/> checks it. A failure is reported on the connection string and gives the reason.
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for a connection string property of the input.</param>
    /// <param name="verification">Opens the connection string and checks the database it reaches.</param>
    /// <param name="tenantId">The tenant the connection string must route to.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptionsConditions<T, string?> MustRouteToTenant<T>(
        this IRuleBuilder<T, string?> rule,
        TenantConnectionVerification verification,
        Func<T, string> tenantId)
    {
        return rule.CustomAsync(async (connectionString, context, cancellationToken) =>
        {
            var check = await verification.CheckAsync(tenantId(context.InstanceToValidate), connectionString!, cancellationToken);
            if (!check.IsValid) context.AddFailure(check.Message);
        });
    }
}
