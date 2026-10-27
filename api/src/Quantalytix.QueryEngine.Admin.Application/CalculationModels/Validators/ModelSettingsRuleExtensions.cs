using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>FluentValidation rules for the settings a model is created with and an <c>updateModel</c> operation changes.</summary>
public static class ModelSettingsRuleExtensions
{
    /// <summary>Requires a model name: not empty and within <c>display_name VARCHAR(256)</c>.</summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for the name.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeModelName<T>(this IRuleBuilder<T, string?> rule)
    {
        return rule.NotEmpty().MustFitVarchar(256);
    }

    /// <summary>Requires an ISO 4217 currency code of three capital letters.</summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for the currency.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeCurrencyCode<T>(this IRuleBuilder<T, string?> rule)
    {
        return rule.Matches("^[A-Z]{3}$").WithMessage("'{PropertyName}' must be an ISO 4217 code of three capital letters, such as 'USD'.");
    }
}
