using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Expressions;

/// <summary>FluentValidation rules for expression text stored in metadata columns.</summary>
public static class ExpressionRuleExtensions
{
    /// <summary>
    /// Requires non-empty text to parse as an expression language 1.0 expression; null or empty text passes. A failure
    /// carries the engine's message, which names the failing character.
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for an expression property of the input.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptionsConditions<T, string?> MustBeValidExpression<T>(this IRuleBuilder<T, string?> rule)
    {
        return rule.Custom((text, context) =>
        {
            if (string.IsNullOrEmpty(text)) return;
            var inspection = ExpressionInspector.Inspect(text);
            if (!inspection.IsValid) context.AddFailure(inspection.Error!);
        });
    }
}
