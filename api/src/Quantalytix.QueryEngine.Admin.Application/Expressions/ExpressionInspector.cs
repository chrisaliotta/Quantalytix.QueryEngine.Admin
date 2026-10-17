using Quantalytix.QueryEngine.Domain.Expressions;

namespace Quantalytix.QueryEngine.Admin.Application.Expressions;

/// <summary>Checks expression text with the query engine's own parser, so the admin API accepts exactly what queries can run.</summary>
public static class ExpressionInspector
{
    /// <summary>Parses an expression and lists the fields, measures, dimensions and request parameters it references.</summary>
    /// <param name="text">Expression language 1.0 text, such as <c>[f.balance] * [m.rate]</c>.</param>
    /// <returns>The references when the text parses; otherwise the engine's error message and position.</returns>
    public static ExpressionInspection Inspect(string text)
    {
        try
        {
            var expression = ExpressionParser.Parse(text);
            return new ExpressionInspection
            {
                IsValid = true,
                FieldKeys = [.. ExpressionTree.FieldKeys(expression).Distinct(StringComparer.Ordinal)],
                MeasureKeys = [.. ExpressionTree.MeasureKeys(expression).Distinct(StringComparer.Ordinal)],
                DimensionKeys = [.. ExpressionTree.DimensionKeys(expression).Distinct(StringComparer.Ordinal)],
                ParameterNames = [.. ExpressionTree.ParameterNames(expression).Distinct(StringComparer.Ordinal)]
            };
        }
        catch (ExpressionSyntaxException exception)
        {
            return new ExpressionInspection
            {
                IsValid = false,
                Error = exception.Message,
                Position = exception.Position,
                FieldKeys = [],
                MeasureKeys = [],
                DimensionKeys = [],
                ParameterNames = []
            };
        }
    }
}
