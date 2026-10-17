using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// Checks the shape of a release's report pins: each report appears once, and each pinned version is a version of its report.
/// Whether the version is published and its sources resolve in the release is for the release's publication rules to decide.
/// </summary>
public sealed class ReleaseReportsInputValidator : AbstractValidator<ReleaseReportsInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="versions">Reads the pinned report versions.</param>
    public ReleaseReportsInputValidator(ITableGateway<ReportVersionRecord> versions)
    {
        RuleForEach(input => input.Reports)
            .Must((input, pin) => input.Reports.Count(other => other.ReportId == pin.ReportId) == 1)
            .WithMessage((_, pin) => $"Report {pin.ReportId} appears more than once.");
        RuleFor(input => input.Reports).CustomAsync(async (pins, context, cancellationToken) =>
        {
            var pinned = (await versions.ListAsync(new { ReportVersionId = pins.Select(pin => pin.ReportVersionId).ToArray() }, cancellationToken))
                .ToDictionary(version => version.ReportVersionId);
            foreach (var pin in pins.Where(pin => !pinned.TryGetValue(pin.ReportVersionId, out var version) || version.ReportId != pin.ReportId))
                context.AddFailure($"Report version {pin.ReportVersionId} is not a version of report {pin.ReportId}.");
        });
    }
}
