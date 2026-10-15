using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Checks the identity attributes of a bridge update; <see cref="NewBridgeInputValidator"/> includes these rules.</summary>
public sealed class BridgeInputValidator : AbstractValidator<BridgeInput>
{
    /// <summary>Declares the rules.</summary>
    public BridgeInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
    }
}
