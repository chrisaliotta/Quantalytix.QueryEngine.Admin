using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// Requires the fields a binding reads, its source field and the interval-end field of its time extension, to be fields of
/// the binding's own dataset version: the query engine resolves both by ID among the version's fields.
/// </summary>
public sealed class BindingFieldsInVersionRule(
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<TimeBindingRecord> timeBindings,
    ITableGateway<FieldRecord> fields,
    BindingNames names) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "binding_fields_in_version";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each source field and interval-end field that is not a field of the version.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per foreign field reference.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var versionFilter = new { DatasetVersionId = versionId };
        var versionBindings = await bindings.ListAsync(versionFilter, cancellationToken);
        var named = await names.OfAsync(versionBindings, cancellationToken);
        var extensions = (await timeBindings.ListAsync(new { BindingId = versionBindings.Select(binding => binding.BindingId).ToArray() }, cancellationToken))
            .ToDictionary(extension => extension.BindingId);
        var fieldIds = (await fields.ListAsync(versionFilter, cancellationToken)).Select(field => field.FieldId).ToHashSet();

        var violations = new List<RuleViolation>();
        foreach (var binding in versionBindings.OrderBy(binding => named[binding.BindingId], StringComparer.Ordinal))
        {
            if (binding.SourceFieldId is { } sourceFieldId && !fieldIds.Contains(sourceFieldId))
            {
                violations.Add(Violation($"Binding '{named[binding.BindingId]}' reads field {sourceFieldId}, which is not a field of this version."));
            }

            if (extensions.TryGetValue(binding.BindingId, out var extension) && extension.IntervalEndFieldId is { } endFieldId && !fieldIds.Contains(endFieldId))
            {
                violations.Add(Violation($"Time binding '{named[binding.BindingId]}' ends its intervals at field {endFieldId}, which is not a field of this version."));
            }
        }

        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
