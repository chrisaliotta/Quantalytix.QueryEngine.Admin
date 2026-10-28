using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Expressions;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>FluentValidation rules for the contract's <c>FilterNode</c>, which the API stores in the engine's shape.</summary>
public static class FilterNodeRuleExtensions
{
    /// <summary>
    /// Requires a filter, when present, to be well formed in the engine's shape and to compare only operands of one type, or also
    /// request parameters other than those calculated measures read per period.
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for a filter property.</param>
    /// <param name="operandType">The operand type the filter compares, such as <c>dimension</c> or <c>field</c>.</param>
    /// <param name="reads">What the filter compares, for the message.</param>
    /// <param name="readsParameters">Whether the filter may also compare request parameters.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptionsConditions<T, FilterNode?> MustBeFilterNodeOver<T>(
        this IRuleBuilder<T, FilterNode?> rule, string operandType, string reads, bool readsParameters = false)
    {
        return rule.Custom((filter, context) =>
        {
            if (ReportFilters.ToEngine(filter) is not { } json) return;
            var inspection = FilterNodes.Inspect(json);
            if (!inspection.IsValid)
            {
                context.AddFailure(inspection.Error!);
                return;
            }

            foreach (var operand in inspection.Operands.Where(operand => operand.Type != operandType))
            {
                if (!readsParameters || operand.Type != "parameter")
                    context.AddFailure($"'{context.DisplayName}' compares {reads} only, not the {operand.Type} '{operand.Id}'.");
                else if (FormulaContext.ParameterNames.Contains(operand.Id))
                    context.AddFailure($"'{context.DisplayName}' cannot compare @{operand.Id}, which calculated measures read for each period.");
            }
        });
    }
}
