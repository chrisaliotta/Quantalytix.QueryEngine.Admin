using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>report_row_parameters_resolve</c>: every parameter value a report's rows set belongs to a row of the report, a member of the
/// report dimension or an overlay row, is for a parameter the report's model declares, and fits the parameter's type and allowed
/// values. The engine hands each row's values, or its nearest ancestor's, to the model's formulas without checking them, and the
/// model's parameters can change after a row sets a value, so the model gate runs the rule over every report of the model.
/// </summary>
public sealed class ReportRowParametersResolveRule : IReportRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "report_row_parameters_resolve";

    /// <summary>Reports each value of a row the report lacks, of a parameter the model lacks, or that the parameter does not take.</summary>
    /// <param name="report">The report, with its model's parameters.</param>
    /// <param name="scope">Unused: rows are the report's own and parameters the model's.</param>
    /// <returns>One violation per problem.</returns>
    public IEnumerable<RuleViolation> Evaluate(ReportGraph report, ReportScope scope)
    {
        var subject = $"Report '{report.ReportKey}'";
        var rowKeys = report.Members.Select(member => member.MemberKey).Concat(report.Lines.Select(line => line.LineKey)).ToHashSet(StringComparer.Ordinal);
        var parameters = report.Parameters.ToDictionary(parameter => parameter.ParameterKey, StringComparer.Ordinal);
        var values = report.RowParameters
            .OrderBy(value => value.RowKey, StringComparer.Ordinal)
            .ThenBy(value => value.ParameterKey, StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!rowKeys.Contains(value.RowKey))
                yield return Violation($"{subject} sets '{value.ParameterKey}' on '{value.RowKey}', which is no row of the report.");
            if (!parameters.TryGetValue(value.ParameterKey, out var parameter))
                yield return Violation($"{subject} row '{value.RowKey}' sets '{value.ParameterKey}', which is no parameter of the report's model.");
            else if (ParameterValues.Problem(parameter, value.NumberValue, value.TextValue) is { } problem)
                yield return Violation($"{subject} row '{value.RowKey}' sets parameter '{value.ParameterKey}', which {problem}.");
        }
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
