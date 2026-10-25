using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>Checks a new release's key, which must be well-formed and unused, and that the release to copy from exists.</summary>
public sealed class ReleaseCreateInputValidator : AbstractValidator<ReleaseCreateInput>
{
    /// <summary>Letters, digits, <c>_</c>, <c>.</c>, <c>-</c> and <c>/</c>, starting with a letter or digit, as in <c>development/baseline</c>.</summary>
    private const string KeyPattern = "^[A-Za-z0-9][A-Za-z0-9_./-]*$";

    /// <summary>Declares the rules.</summary>
    /// <param name="releases">The release table, read for the uniqueness and existence checks.</param>
    public ReleaseCreateInputValidator(ITableGateway<SemanticReleaseRecord> releases)
    {
        RuleFor(input => input.ReleaseKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(128)
            .Matches(KeyPattern)
            .WithMessage("'releaseKey' must start with a letter or digit and contain only letters, digits, '_', '.', '-' and '/'.")
            .MustAsync(async (key, cancellationToken) => (await releases.ListAsync(new { ReleaseKey = key }, cancellationToken)).Count == 0)
            .WithMessage(input => $"A release with key '{input.ReleaseKey}' already exists.");
        RuleFor(input => input.CopyFromSemanticReleaseId)
            .MustAsync(async (id, cancellationToken) => (await releases.ListAsync(new { SemanticReleaseId = id }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Release {input.CopyFromSemanticReleaseId} does not exist.")
            .When(input => input.CopyFromSemanticReleaseId is not null);
    }
}
