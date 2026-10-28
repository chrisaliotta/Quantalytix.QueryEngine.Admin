using System.Linq.Expressions;
using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>FluentValidation rules the dataset inputs share.</summary>
public static class DatasetRuleExtensions
{
    /// <summary>Requires a dataset key: the contract's <c>DottedKey</c>, fitting its column.</summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for the key.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, string> MustBeDatasetKey<T>(this IRuleBuilder<T, string> rule)
    {
        return rule.NotEmpty().MaximumLength(DatasetPatterns.DatasetKeyLength).Matches(DatasetPatterns.DatasetKey()).WithMessage(DatasetPatterns.DatasetKeyMessage);
    }

    /// <summary>Requires a physical table or column name that can be inlined into DDL.</summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for the name.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeIdentifier<T>(this IRuleBuilder<T, string?> rule)
    {
        return rule.MaximumLength(DatasetPatterns.IdentifierLength).Matches(DatasetPatterns.Identifier()).WithMessage(DatasetPatterns.IdentifierMessage);
    }

    /// <summary>
    /// Requires a name for a table the API creates: an identifier outside the query engine's own <c>qe_</c> names. Put
    /// <c>Cascade(CascadeMode.Stop)</c> in front so an empty name reports one failure.
    /// </summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="rule">The rule for the table name.</param>
    /// <returns>The rule, for further configuration.</returns>
    public static IRuleBuilderOptions<T, string?> MustBeTableName<T>(this IRuleBuilder<T, string> rule)
    {
        return rule
            .NotEmpty()
            .MustBeIdentifier()
            .Must(name => !SourceObjects.SourceObjectDirectory.IsMetadata(name!))
            .WithMessage("'{PropertyName}' must not start with 'qe_' or 'vw_qe_', which name the query engine's own objects.");
    }

    /// <summary>Requires a field type: a known data type, with a precision and scale exactly when decimal.</summary>
    /// <typeparam name="T">The validated input type.</typeparam>
    /// <param name="validator">The validator to add the rules to.</param>
    /// <param name="dataType">Reads the data type.</param>
    /// <param name="precision">Reads the precision.</param>
    /// <param name="scale">Reads the scale.</param>
    public static void RequireFieldType<T>(
        this AbstractValidator<T> validator,
        Expression<Func<T, string>> dataType,
        Expression<Func<T, int?>> precision,
        Expression<Func<T, int?>> scale)
    {
        var typeOf = dataType.Compile();
        validator.RuleFor(dataType).MustBeIn(DataTypes.Definition);
        validator.When(input => typeOf(input) == DataTypes.Decimal, () =>
        {
            validator.RuleFor(precision).MustBeDecimalPrecision();
            validator.RuleFor(scale).MustBeDecimalScale(precision.Compile(), "precision");
        }).Otherwise(() =>
        {
            validator.RuleFor(precision).Null().WithMessage("Only decimal fields have a precision, so '{PropertyName}' must be empty.");
            validator.RuleFor(scale).Null().WithMessage("Only decimal fields have a scale, so '{PropertyName}' must be empty.");
        });
    }
}
