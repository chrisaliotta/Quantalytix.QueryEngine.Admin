using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a classification against the canonical dimension it classifies into, which never changes: the report's dimension for a
/// source, the calculation dimension's dimension for a route. Each kind names exactly its own columns: a mapping set into the target
/// for <c>mapped</c>, a bridge into the target for <c>bridge</c>, neither otherwise. A <c>field</c> route reads raw values, so it is
/// the one kind a scalar calculation dimension, which has no target, takes. Whether the field and its binding exist in the dataset
/// version a release pins is a release rule. Internal, so the validators that build it with their own target are the only ones
/// registered.
/// </summary>
internal sealed class ClassificationInputValidator : AbstractValidator<ClassificationInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="kinds">The kinds the classification may have: <c>classification_kind</c> for a source, <c>route_kind</c> for a route.</param>
    /// <param name="target">Reads the dimension the classification targets; null for a scalar calculation dimension.</param>
    /// <param name="mappingSets">Reads the mapping set.</param>
    /// <param name="bridges">Reads the bridge.</param>
    public ClassificationInputValidator(
        Vocabulary.Vocabulary kinds,
        Func<CancellationToken, Task<long?>> target,
        ITableGateway<MappingSetRecord> mappingSets,
        ITableGateway<BridgeRecord> bridges)
    {
        RuleFor(classification => classification.Kind)
            .Cascade(CascadeMode.Stop)
            .MustBeIn(kinds)
            .MustAsync(async (kind, cancellationToken) => (kind == RouteKinds.Field) == (await target(cancellationToken) is null))
            .WithMessage(classification => classification.Kind == RouteKinds.Field
                ? "A field route reads raw values, so only a scalar calculation dimension takes one."
                : "A scalar calculation dimension reads raw values, so its routes are field routes.");
        RuleFor(classification => classification.FieldKey).NotEmpty().MustFitVarchar(128);
        When(classification => classification.Kind == ClassificationKinds.Mapped, () =>
        {
            RuleFor(classification => classification.MappingSetId)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage("'{PropertyName}' is required for a mapped classification.")
                .MustAsync(async (mappingSetId, cancellationToken) =>
                    (await mappingSets.ListAsync(new { MappingSetId = mappingSetId, DimensionId = await target(cancellationToken) }, cancellationToken)).Count == 1)
                .WithMessage(classification => $"Mapping set {classification.MappingSetId} does not map into the dimension this classification targets.");
        }).Otherwise(() =>
        {
            RuleFor(classification => classification.MappingSetId).Null().WithMessage("Only mapped classifications use '{PropertyName}', so it must be empty.");
        });
        When(classification => classification.Kind == ClassificationKinds.Bridge, () =>
        {
            RuleFor(classification => classification.BridgeId)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage("'{PropertyName}' is required for a bridge classification.")
                .MustAsync(async (bridgeId, cancellationToken) =>
                    (await bridges.ListAsync(new { BridgeId = bridgeId, TargetDimensionId = await target(cancellationToken) }, cancellationToken)).Count == 1)
                .WithMessage(classification => $"Bridge {classification.BridgeId} does not lead into the dimension this classification targets.");
        }).Otherwise(() =>
        {
            RuleFor(classification => classification.BridgeId).Null().WithMessage("Only bridge classifications use '{PropertyName}', so it must be empty.");
        });
    }
}
