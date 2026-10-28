using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Checks a new mapping set's shape: a well-formed dotted key (B14) and names that fit their columns. A taken key or a dimension that
/// cannot take mapping sets (B1) is a conflict, which the command answers.
/// </summary>
public sealed class CreateMappingSetInputValidator : AbstractValidator<CreateMappingSetInput>
{
    /// <summary>Declares the rules.</summary>
    public CreateMappingSetInputValidator()
    {
        RuleFor(input => input.Key).Cascade(CascadeMode.Stop).MustBeDottedKey();
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.SourceNamespace).NotEmpty().MustFitVarchar(128);
    }
}
