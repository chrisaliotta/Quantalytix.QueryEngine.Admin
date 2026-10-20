using FluentValidation;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// Checks a variable source's classification against the report's dimension, which never changes. Each kind names exactly its own
/// columns: a field for <c>direct</c> and <c>mapped</c>, plus a mapping set into the report's dimension for <c>mapped</c>; a role
/// and a bridge into the report's dimension for <c>bridge</c>, whose source dimension is the role's template. Whether the field
/// exists in the dataset version a release pins, or that version binds the role, is a release rule. Internal, so the validator
/// that builds it with the report's dimension is the only one registered.
/// </summary>
internal sealed class ClassificationInputValidator : AbstractValidator<ClassificationInput>
{
    /// <summary>Declares the rules.</summary>
    /// <param name="target">Reads the report's dimension, which the classification targets.</param>
    /// <param name="mappingSets">Reads the mapping set.</param>
    /// <param name="bridges">Reads the bridge.</param>
    /// <param name="roles">Reads the role.</param>
    public ClassificationInputValidator(
        Func<CancellationToken, Task<long>> target,
        ITableGateway<MappingSetRecord> mappingSets,
        ITableGateway<BridgeRecord> bridges,
        ITableGateway<DimensionRoleRecord> roles)
    {
        RuleFor(classification => classification.Kind).MustBeIn(ClassificationKinds.Definition);
        When(classification => classification.Kind == ClassificationKinds.Mapped, () =>
        {
            RuleFor(classification => classification.MappingSetId)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage("'{PropertyName}' is required for a mapped classification.")
                .MustAsync(async (mappingSetId, cancellationToken) =>
                    (await mappingSets.ListAsync(new { MappingSetId = mappingSetId, DimensionId = await target(cancellationToken) }, cancellationToken)).Count == 1)
                .WithMessage(classification => $"Mapping set {classification.MappingSetId} does not map into the report's dimension.");
        }).Otherwise(() =>
        {
            RuleFor(classification => classification.MappingSetId).Null().WithMessage("Only mapped classifications use '{PropertyName}', so it must be empty.");
        });
        When(classification => classification.Kind == ClassificationKinds.Bridge, () =>
        {
            RuleFor(classification => classification.FieldKey).Null().WithMessage("A bridge classification reads its role's binding, so '{PropertyName}' must be empty.");
            RuleFor(classification => classification.BridgeId)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage("'{PropertyName}' is required for a bridge classification.")
                .MustAsync(async (bridgeId, cancellationToken) =>
                    (await bridges.ListAsync(new { BridgeId = bridgeId, TargetDimensionId = await target(cancellationToken) }, cancellationToken)).Count == 1)
                .WithMessage(classification => $"Bridge {classification.BridgeId} does not lead into the report's dimension.");
            RuleFor(classification => classification.RoleId)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage("'{PropertyName}' is required for a bridge classification.")
                .MustAsync(async (classification, roleId, cancellationToken) =>
                {
                    var bridge = await bridges.ListAsync(new { classification.BridgeId }, cancellationToken);
                    var from = bridge.Select(row => row.SourceDimensionId).ToArray();
                    return (await roles.ListAsync(new { RoleId = roleId, DimensionId = from }, cancellationToken)).Count == 1;
                })
                .WithMessage(classification => $"Role {classification.RoleId} is no role of the source dimension of bridge {classification.BridgeId}.")
                .When(classification => classification.BridgeId is not null, ApplyConditionTo.CurrentValidator);
        }).Otherwise(() =>
        {
            RuleFor(classification => classification.FieldKey).NotEmpty().MustFitVarchar(128);
            RuleFor(classification => classification.RoleId).Null().WithMessage("Only bridge classifications use '{PropertyName}', so it must be empty.");
            RuleFor(classification => classification.BridgeId).Null().WithMessage("Only bridge classifications use '{PropertyName}', so it must be empty.");
        });
    }
}
