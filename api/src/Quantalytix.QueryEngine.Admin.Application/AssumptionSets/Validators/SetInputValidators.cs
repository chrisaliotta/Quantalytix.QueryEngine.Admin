using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>Checks a capture: a well-formed key (R11) and labels that fit their columns. A taken key is a conflict.</summary>
public sealed class CaptureSetInputValidator : AbstractValidator<CaptureSetInput>
{
    /// <summary>The contract's <c>AssumptionSetKey</c>: a letter, then letters, digits or underscores, 128 at most.</summary>
    private const string KeyPattern = "^[A-Za-z][A-Za-z0-9_]{0,127}$";

    /// <summary>Declares the rules.</summary>
    public CaptureSetInputValidator()
    {
        RuleFor(input => input.Key).Matches(KeyPattern).WithMessage("Use a letter, then letters, digits or underscores (128 at most).");
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.Description).MustFitVarchar(1024);
    }
}

/// <summary>Checks a relabel: labels that fit their columns.</summary>
public sealed class RelabelSetInputValidator : AbstractValidator<RelabelSetInput>
{
    /// <summary>Declares the rules.</summary>
    public RelabelSetInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.Description).MustFitVarchar(1024);
    }
}
