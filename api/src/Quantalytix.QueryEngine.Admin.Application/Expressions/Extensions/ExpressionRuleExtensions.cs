using FluentValidation;
using Quantalytix.QueryEngine.Domain.Expressions;

namespace Quantalytix.QueryEngine.Admin.Application.Expressions;

/// <summary>FluentValidation rules for expression text stored in metadata columns.</summary>
public static class ExpressionRuleExtensions
{
    /// <summary>
    /// Requires non-empty text to parse as an expression language 1.0 expression that reads no <c>@name</c> request
    /// parameters and no <c>[d.key]</c> dimension members; null or empty text passes. A syntax failure carries the engine's
    /// message, which names the failing character. Metadata expressions read no request parameters: the query engine renders
    /// field expressions and row filters with none bound, and the catalog declares none that a request would know to send for
    /// a formula. Dimension member references belong to a query's <c>where</c> filter, which resolves members first. Every
    /// dataset version's expressions are language 1.0, so they call no <c>POWER</c>, which only language 1.1 has, and no
    /// <c>YEARFRAC</c>, which only model formulas of formula language 1.2 call.
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

            if (inspection.DimensionKeys.Count > 0)
            {
                var dimensions = string.Join(", ", inspection.DimensionKeys.Select(key => $"'[d.{key}]'"));
                context.AddFailure($"Metadata expressions read fields, not dimension members, so '{context.DisplayName}' cannot read {dimensions}.");
            }

            if (inspection.Functions.Contains(ExpressionFunction.Power))
                context.AddFailure($"Metadata expressions are expression language 1.0, which has no POWER, so '{context.DisplayName}' cannot call it.");
            if (inspection.Functions.Contains(ExpressionFunction.YearFrac))
                context.AddFailure($"Metadata expressions are expression language 1.0, which has no YEARFRAC, so '{context.DisplayName}' cannot call it.");

            if (inspection.ParameterNames.Count == 0) return;
            var parameters = string.Join(", ", inspection.ParameterNames.Select(name => $"'@{name}'"));
            context.AddFailure($"Metadata expressions bind no request parameters, so '{context.DisplayName}' cannot read {parameters}.");
        });
    }

    /// <summary>
    /// Requires a model formula, a calculated measure's formula or a metric's attribution expression, to parse and to read no
    /// dataset fields or dimension members: a model formula is evaluated over aligned report values, so it reads <c>[m.KEY]</c>
    /// model keys and <c>@name</c> parameters only. Whether each key names a metric of the model, whether each parameter is a context
    /// parameter or one the model declares and is read where its type belongs, whether the formulas are acyclic, and whether the
    /// model's formula language has what they use, are model rules, because later edits to the model can change every answer.
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for the formula text.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptionsConditions<T, string?> MustBeModelFormula<T>(this IRuleBuilder<T, string?> rule)
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

            if (inspection.FieldKeys.Count > 0 || inspection.DimensionKeys.Count > 0)
                context.AddFailure("A model formula reads variables and calculated measures through [m.KEY], not dataset fields or dimension members.");
        });
    }

    /// <summary>
    /// Requires a structured filter to be a well-formed <c>filterNode</c> whose operands are all of one type, such as a
    /// source variable's filter over dataset fields or a report's default filter over calculation dimensions; null passes.
    /// Whether each key resolves depends on what the filter reads and is checked where that is known.
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for the filter's JSON text.</param>
    /// <param name="operandType">The only operand type the filter may compare: <c>field</c> or <c>dimension</c>.</param>
    /// <param name="reads">What the filter reads, for the message, such as <c>the dataset's fields</c>.</param>
    /// <param name="readsParameters">Whether the filter may also compare <c>parameter</c> operands, which every request then supplies,
    /// as a report's default filter may. <c>periodDays</c> and <c>yearDays</c> stay the calculated measures' own.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptionsConditions<T, string?> MustBeFilterOver<T>(
        this IRuleBuilder<T, string?> rule, string operandType, string reads, bool readsParameters = false)
    {
        return rule.Custom((json, context) =>
        {
            if (json is null) return;
            var inspection = FilterNodes.Inspect(json);
            if (!inspection.IsValid)
            {
                context.AddFailure(inspection.Error!);
                return;
            }

            foreach (var operand in inspection.Operands.Where(operand => operand.Type != operandType))
            {
                if (readsParameters && operand.Type == "parameter")
                {
                    if (FormulaContext.ParameterNames.Contains(operand.Id))
                        context.AddFailure($"'{context.DisplayName}' cannot compare @{operand.Id}, which calculated measures read for each period.");
                }
                else context.AddFailure($"'{context.DisplayName}' compares {reads} only, not the {operand.Type} '{operand.Id}'.");
            }
        });
    }
}
