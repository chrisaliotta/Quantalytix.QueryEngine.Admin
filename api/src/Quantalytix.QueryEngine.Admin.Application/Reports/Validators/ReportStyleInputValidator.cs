using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a style's fields: a font size from 6 to 72 points, <c>#RRGGBB</c> colors, weights, borders and the default role from their
/// vocabularies, and an indent from 0 to 10. Whether another style of the report is already its role's default is a report rule.
/// </summary>
public sealed class ReportStyleInputValidator : AbstractValidator<ReportStyleInput>
{
    /// <summary>A <c>#RRGGBB</c> color, matching the whole text.</summary>
    private const string ColorPattern = @"^#[0-9A-Fa-f]{6}\z";

    /// <summary>Declares the rules.</summary>
    public ReportStyleInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.DefaultRole).MustBeIn(RowRoles.Definition);
        RuleFor(input => input.FontFamily).NotEmpty().MustFitVarchar(128).When(input => input.FontFamily is not null);
        RuleFor(input => input.FontSize).InclusiveBetween(6m, 72m).PrecisionScale(5, 2, true);
        RuleFor(input => input.FontWeight).MustBeIn(FontWeights.Definition);
        RuleFor(input => input.TextColor).Matches(ColorPattern).WithMessage("'{PropertyName}' must be a color such as '#1F2937'.");
        RuleFor(input => input.BackgroundColor).Matches(ColorPattern).WithMessage("'{PropertyName}' must be a color such as '#F3F4F6'.");
        RuleFor(input => input.BorderTop).MustBeIn(BorderStyles.Definition);
        RuleFor(input => input.BorderBottom).MustBeIn(BorderStyles.Definition);
        RuleFor(input => input.Indent).InclusiveBetween(0, 10);
    }
}
