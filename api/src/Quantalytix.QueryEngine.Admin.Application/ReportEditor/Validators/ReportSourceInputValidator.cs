using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Content;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// Checks an edit of a report source: a label, and a time field with a time binding in the active release's version of the
/// source's dataset, which reports run against.
/// </summary>
public sealed class ReportSourceInputValidator : AbstractValidator<ReportSourceInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="sources">Reads the route's source.</param>
    /// <param name="releaseDatasets">Reads the active release's version of the source's dataset.</param>
    /// <param name="keys">Names the route's source.</param>
    public ReportSourceInputValidator(ITableGateway<ReportSourceRecord> sources, ReleaseDatasets releaseDatasets, ContentKeys keys)
    {
        RuleFor(input => input.DisplayName).NotEmpty().MustFitVarchar(256);
        RuleFor(input => input.TimeFieldKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustFitVarchar(128)
            .MustAsync(async (key, cancellationToken) =>
            {
                var source = await sources.GetAsync(new { ReportSourceId = keys.SourceId }, cancellationToken);
                return (await releaseDatasets.ActiveAsync(source.DatasetId, cancellationToken))?.IsTimeField(key!) == true;
            })
            .WithMessage(input => $"Field '{input.TimeFieldKey}' has no time binding in the active release's version of the source's dataset.");
    }
}
