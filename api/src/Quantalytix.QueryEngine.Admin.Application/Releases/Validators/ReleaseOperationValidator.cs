using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>Checks a manifest operation's shape: one row per dataset. Whether each row's version may be pinned is a release gate.</summary>
public sealed class ReleaseOperationValidator : AbstractValidator<ReleaseOperation>
{
    /// <summary>Declares the rules.</summary>
    public ReleaseOperationValidator()
    {
        RuleFor(operation => operation).SetInheritanceValidator(operations =>
        {
            operations.Add(new InlineValidator<ReleaseSaveManifestOperation>
            {
                validator => validator.RuleFor(operation => operation.Manifest)
                    .Must(manifest => manifest.DistinctBy(entry => entry.DatasetId).Count() == manifest.Count)
                    .WithMessage("Pin each dataset once.")
            });
        });
    }
}
