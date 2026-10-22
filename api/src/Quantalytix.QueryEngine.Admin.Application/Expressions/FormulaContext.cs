namespace Quantalytix.QueryEngine.Admin.Application.Expressions;

/// <summary>
/// The context parameters every model formula may read in every formula language: <c>@periodDays</c>, the days of the period a
/// report evaluates the formula for, and <c>@yearDays</c>, the days of that period's year. No model declares a parameter by either name.
/// </summary>
public static class FormulaContext
{
    /// <summary>The context parameters' names, without the <c>@</c>, in the order the <c>days</c> driver lists them.</summary>
    public static readonly IReadOnlyList<string> ParameterNames = ["periodDays", "yearDays"];
}
