using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>Checks a new release: a well-formed key (R7) and, when given, a release to copy that exists. A taken key is a conflict.</summary>
public sealed class CreateReleaseInputValidator : AbstractValidator<CreateReleaseInput>
{
    /// <summary>The contract's <c>ReleaseKey</c>: letters, digits, <c>_ . - /</c>, starting with a letter or digit, 128 at most.</summary>
    private const string KeyPattern = @"^[A-Za-z0-9][A-Za-z0-9_.\-/]{0,127}$";

    /// <summary>Declares the rules.</summary>
    /// <param name="releases">The release table, read for the existence check.</param>
    public CreateReleaseInputValidator(ITableGateway<SemanticReleaseRecord> releases)
    {
        RuleFor(input => input.Key)
            .Matches(KeyPattern)
            .WithMessage("Use letters, digits, _ . - /, starting with a letter or digit (128 at most).");
        RuleFor(input => input.CopyFromReleaseId)
            .MustAsync(async (id, cancellationToken) => (await releases.ListAsync(new { SemanticReleaseId = id }, cancellationToken)).Count == 1)
            .WithMessage(input => $"Release {input.CopyFromReleaseId} does not exist.")
            .When(input => input.CopyFromReleaseId is not null);
    }
}
