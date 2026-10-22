using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Whether a value fits a model parameter: a number for a number parameter, and text for a text parameter, one of its allowed values
/// when it has them. A report row's value and a parameter's default are checked alike.
/// </summary>
public static class ParameterValues
{
    /// <summary>What keeps a parameter from taking a value, which is a number or text and never both; null when it takes it.</summary>
    /// <param name="parameter">The declared parameter.</param>
    /// <param name="number">The value when it is a number; otherwise null.</param>
    /// <param name="text">The value when it is text; otherwise null.</param>
    /// <returns>A clause completing "which ...", such as <c>is a number parameter, so its value must be a number</c>; null when the value fits.</returns>
    public static string? Problem(ModelParameter parameter, decimal? number, string? text)
    {
        if (parameter.ValueType == ParameterValueTypes.Number)
            return number is null || text is not null ? "is a number parameter, so its value must be a number" : null;
        if (text is null || number is not null) return "is a text parameter, so its value must be text";
        if (parameter.AllowedValues is { } allowed && !allowed.Contains(text, StringComparer.Ordinal))
            return $"takes only {string.Join(", ", allowed.Select(value => $"'{value}'"))}, not '{text}'";
        return null;
    }
}
