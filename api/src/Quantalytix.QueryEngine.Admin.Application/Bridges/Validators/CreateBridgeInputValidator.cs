using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>
/// Checks a new analytical bridge's shape: a well-formed dotted key (B14) and a name that fits its column. A taken key or endpoints a
/// bridge cannot join (B4) are conflicts, which the command answers.
/// </summary>
public sealed class CreateBridgeInputValidator : AbstractValidator<CreateBridgeInput>
{
    /// <summary>Declares the rules.</summary>
    public CreateBridgeInputValidator()
    {
        RuleFor(input => input.Key).Cascade(CascadeMode.Stop).MustBeDottedKey(MappingRuleExtensions.KeyLength);
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
    }
}
