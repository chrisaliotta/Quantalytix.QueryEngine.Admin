using FluentValidation;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Checks the bridge list filters: a report and its source key go together, and never with a dimension.</summary>
public sealed class BridgeListQueryValidator : AbstractValidator<BridgeListQuery>
{
    /// <summary>Declares the rules.</summary>
    public BridgeListQueryValidator()
    {
        RuleFor(query => query.SourceKey).NotEmpty().WithMessage("'{PropertyName}' is required with 'reportId'.").When(query => query.ReportId is not null);
        RuleFor(query => query.ReportId).NotNull().WithMessage("'{PropertyName}' is required with 'sourceKey'.").When(query => query.SourceKey is not null);
        RuleFor(query => query.DimensionId).Null().WithMessage("Filter by a dimension or by a report source, not both.").When(query => query.ReportId is not null);
    }
}
