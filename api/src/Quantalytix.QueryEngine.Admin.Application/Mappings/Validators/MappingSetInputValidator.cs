using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>Checks the identity attributes of a mapping set update; <see cref="NewMappingSetInputValidator"/> includes these rules.</summary>
public sealed class MappingSetInputValidator : AbstractValidator<MappingSetInput>
{
    /// <summary>Declares the rules.</summary>
    public MappingSetInputValidator()
    {
        RuleFor(input => input.SourceNamespace).NotEmpty().MustFitVarchar(128);
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
    }
}
