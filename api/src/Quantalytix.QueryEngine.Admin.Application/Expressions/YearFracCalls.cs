using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Domain.Expressions;

namespace Quantalytix.QueryEngine.Admin.Application.Expressions;

/// <summary>The checks of <c>YEARFRAC(convention)</c> that model formulas and dataset measure formulas share.</summary>
public static class YearFracCalls
{
    /// <summary>The problem of a convention given as a string literal, completing a sentence that starts with the expression's subject.</summary>
    /// <param name="text">The literal's text.</param>
    /// <returns>Null for a day count convention; otherwise a phrase such as <c>calls YEARFRAC with 'x', which is no day count convention</c>.</returns>
    public static string? LiteralProblem(string text)
    {
        return DayCountConventions.Contains(text) ? null : $"calls YEARFRAC with '{text}', which is no day count convention";
    }

    /// <summary>
    /// The problems of a dataset measure formula's <c>YEARFRAC</c> calls, which bind no parameters and so take their convention in
    /// quotes; each completes a sentence that starts with the formula's subject.
    /// </summary>
    /// <param name="expression">The parsed formula.</param>
    /// <returns>One problem per call whose argument is not a day count convention in quotes.</returns>
    public static IEnumerable<string> LiteralProblems(Expression expression)
    {
        return ExpressionTree.Nodes(expression)
            .OfType<FunctionCall>()
            .Where(call => call.Function == ExpressionFunction.YearFrac)
            .Select(call => call.Arguments[0] is Literal { Value: string text }
                ? LiteralProblem(text)
                : $"calls YEARFRAC with something other than a day count convention, which it takes in quotes, such as '{DayCountConventions.Actual365}'")
            .OfType<string>();
    }
}
