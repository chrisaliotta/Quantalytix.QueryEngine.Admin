using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Expressions;

/// <summary>FluentValidation rules for expression text stored in metadata columns.</summary>
public static class ExpressionRuleExtensions
{
    /// <summary>
    /// Requires non-empty text to parse as an expression language 1.0 expression that reads no <c>@name</c> request
    /// parameters; null or empty text passes. A syntax failure carries the engine's message, which names the failing
    /// character. Metadata expressions read no request parameters: the query engine renders field expressions and row
    /// filters with none bound, and the catalog declares none that a request would know to send for a formula.
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
            if (!inspection.IsValid)
            {
                context.AddFailure(inspection.Error!);
                return;
            }

            if (inspection.ParameterNames.Count == 0) return;
            var parameters = string.Join(", ", inspection.ParameterNames.Select(name => $"'@{name}'"));
            context.AddFailure($"Metadata expressions bind no request parameters, so '{context.DisplayName}' cannot read {parameters}.");
        });
    }
}
