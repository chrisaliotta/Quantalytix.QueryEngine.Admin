using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>Keeps a freeze note within the <c>freeze_note</c> column.</summary>
public sealed class FreezeInputValidator : AbstractValidator<FreezeInput>
{
    /// <summary>Declares the rules.</summary>
    public FreezeInputValidator()
    {
        RuleFor(input => input.Note).MustFitVarchar(512);
    }
}
