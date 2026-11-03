using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Snapshots;

/// <summary>Checks a capture: a well-formed key and labels that fit their columns. A taken key is a conflict.</summary>
public sealed class CaptureSnapshotInputValidator : AbstractValidator<CaptureSnapshotInput>
{
    /// <summary>The contract's <c>SnapshotKey</c>: a letter, then letters, digits, underscores or dots, 128 at most.</summary>
    private const string KeyPattern = "^[A-Za-z][A-Za-z0-9_.]{0,127}$";

    /// <summary>Declares the rules.</summary>
    public CaptureSnapshotInputValidator()
    {
        RuleFor(input => input.Key).Matches(KeyPattern).WithMessage("Use a letter, then letters, digits, underscores or dots (128 at most).");
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.Description).MustFitVarchar(1024);
    }
}

/// <summary>Checks a relabel: labels that fit their columns.</summary>
public sealed class RelabelSnapshotInputValidator : AbstractValidator<RelabelSnapshotInput>
{
    /// <summary>Declares the rules.</summary>
    public RelabelSnapshotInputValidator()
    {
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.Description).MustFitVarchar(1024);
    }
}
