using System.Globalization;
using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Metadata;

/// <summary>FluentValidation rules for the precision and scale of a StarRocks decimal.</summary>
public static class DecimalRuleExtensions
{
    /// <summary>The largest decimal precision StarRocks supports.</summary>
    private const int MaxPrecision = 38;

    /// <summary>
    /// Whether a value fits a <c>DECIMAL(precision, scale)</c> column: at most <paramref name="scale"/> decimals once trailing zeros
    /// are dropped, and at most <paramref name="precision"/> minus <paramref name="scale"/> digits before the point. Use it where a
    /// value is not a property a rule can name, such as a value in a JSON map; a property keeps FluentValidation's <c>PrecisionScale</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="precision">The column's precision.</param>
    /// <param name="scale">The column's scale.</param>
    /// <returns>True when the column holds the value exactly.</returns>
    public static bool Fits(decimal value, int precision, int scale)
    {
        return decimal.Round(value, scale) == value
            && decimal.Truncate(Math.Abs(value)).ToString(CultureInfo.InvariantCulture).Length <= precision - scale;
    }

    /// <summary>Requires a decimal precision StarRocks supports: given, from 1 to 38.</summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for a precision property of the input.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, int?> MustBeDecimalPrecision<T>(this IRuleBuilder<T, int?> rule)
    {
        return rule.NotNull().InclusiveBetween(1, MaxPrecision);
    }

    /// <summary>
    /// Requires a decimal scale: given, and from 0 up to the precision. The scale is compared only with a given precision, so a
    /// missing precision is reported on the precision alone rather than also as a scale that exceeds it.
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for a scale property of the input.</param>
    /// <param name="precision">Reads the precision the scale belongs to.</param>
    /// <param name="precisionName">The precision's name in the message, such as <c>output precision</c>.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, int?> MustBeDecimalScale<T>(this IRuleBuilderInitial<T, int?> rule, Func<T, int?> precision, string precisionName)
    {
        return rule
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .GreaterThanOrEqualTo(0)
            .Must((input, scale) => scale <= precision(input))
            .WithMessage($"'{{PropertyName}}' must not exceed the {precisionName}.")
            .When(input => precision(input) is not null, ApplyConditionTo.CurrentValidator);
    }
}
