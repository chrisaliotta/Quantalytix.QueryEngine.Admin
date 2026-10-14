using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>Checks a new assumption set: a well-formed, unused key, a display name, and a UTC effective time.</summary>
public sealed class AssumptionSetCreateInputValidator : AbstractValidator<AssumptionSetCreateInput>
{
    /// <summary>A letter, then letters, digits and <c>_</c>, as in <c>FY2026_Q3_CLOSE</c>.</summary>
    private const string KeyPattern = "^[A-Za-z][A-Za-z0-9_]*$";

    /// <summary>Declares the rules.</summary>
    /// <param name="sets">The assumption set table, read for the uniqueness check.</param>
    public AssumptionSetCreateInputValidator(ITableGateway<AssumptionSetRecord> sets)
    {
        RuleFor(input => input.AssumptionSetKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(128)
            .Matches(KeyPattern)
            .WithMessage("'assumptionSetKey' must start with a letter and contain only letters, digits and '_'.")
            .MustAsync(async (key, cancellationToken) => (await sets.ListAsync(new { AssumptionSetKey = key }, cancellationToken)).Count == 0)
            .WithMessage(input => $"An assumption set with key '{input.AssumptionSetKey}' already exists.");
        RuleFor(input => input.DisplayName).NotEmpty().MaximumLength(256);
        RuleFor(input => input.Description).MaximumLength(1024);
        RuleFor(input => input.EffectiveAt).MustBeUtc();
    }
}
