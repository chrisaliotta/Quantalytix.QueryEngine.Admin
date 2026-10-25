using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// Checks the shape of a manifest: every dataset exists, and each dataset and each dataset version appears at most once.
/// Whether a pinned version belongs to its dataset and is published is for the release's publication rules to decide.
/// </summary>
public sealed class ReleaseManifestInputValidator : AbstractValidator<ReleaseManifestInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="datasets">The dataset table, read to check that every dataset exists.</param>
    public ReleaseManifestInputValidator(ITableGateway<DatasetRecord> datasets)
    {
        RuleForEach(input => input.Datasets)
            .Must((input, row) => input.Datasets.Count(other => other.DatasetId == row.DatasetId) == 1)
            .WithMessage((_, row) => $"Dataset {row.DatasetId} appears more than once.")
            .Must((input, row) => input.Datasets.Count(other => other.DatasetVersionId == row.DatasetVersionId) == 1)
            .WithMessage((_, row) => $"Dataset version {row.DatasetVersionId} appears more than once.");
        RuleFor(input => input.Datasets).CustomAsync(async (rows, context, cancellationToken) =>
        {
            long[] datasetIds = [.. rows.Select(row => row.DatasetId)];
            var existing = await datasets.ListAsync(new { DatasetId = datasetIds }, cancellationToken);
            foreach (var missing in datasetIds.Except(existing.Select(dataset => dataset.DatasetId)))
                context.AddFailure($"Dataset {missing} does not exist.");
        });
    }
}
