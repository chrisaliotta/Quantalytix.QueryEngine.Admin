using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>Accepts any lifecycle status; whether the transition is allowed for the version's kind is the workflow's decision.</summary>
public sealed class StatusChangeInputValidator : AbstractValidator<StatusChangeInput>
{
    /// <summary>Declares the rules.</summary>
    public StatusChangeInputValidator()
    {
        RuleFor(input => input.Status).MustBeIn(PublicationStatuses.Definition);
    }
}
