using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Checks a grain's editable attributes: a known grain kind and a non-empty list that names each field once. Whether the
/// fields belong to the version is the <c>grain_fields_in_version</c> publication rule.
/// </summary>
public sealed class GrainUpdateInputValidator : AbstractValidator<GrainUpdateInput>
{
    /// <summary>Declares the rules.</summary>
    public GrainUpdateInputValidator()
    {
        RuleFor(input => input.GrainKind).MustBeIn(GrainKinds.Definition);
        RuleFor(input => input.FieldIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(fieldIds => fieldIds.Distinct().Count() == fieldIds.Count)
            .WithMessage("'{PropertyName}' must name each field once.");
    }
}
