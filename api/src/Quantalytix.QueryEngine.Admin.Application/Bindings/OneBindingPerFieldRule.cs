using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// Allows at most one binding per source field, so each request-facing field ID has one meaning. Disabled bindings count
/// too: the query engine loads every binding of a version and keys them by their field's key, so a second binding of the
/// same field fails the whole version's metadata load whether or not it is enabled.
/// </summary>
public sealed class OneBindingPerFieldRule(ITableGateway<DimensionBindingRecord> bindings, ITableGateway<FieldRecord> fields) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "one_binding_per_field";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each source field that more than one binding of the version reads.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per field with several bindings, naming them.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var versionFilter = new { DatasetVersionId = versionId };
        var versionBindings = await bindings.ListAsync(versionFilter, cancellationToken);
        var fieldKeys = (await fields.ListAsync(versionFilter, cancellationToken)).ToDictionary(field => field.FieldId, field => field.FieldKey);
        return
        [
            .. versionBindings
                .Where(binding => binding.SourceFieldId is not null)
                .GroupBy(binding => binding.SourceFieldId!.Value)
                .Where(group => group.Count() > 1)
                .Select(group => (Field: FieldName(fieldKeys, group.Key), Bindings: group.Select(binding => $"'{binding.DisplayName}'").Order(StringComparer.Ordinal)))
                .OrderBy(shared => shared.Field, StringComparer.Ordinal)
                .Select(shared => new RuleViolation
                {
                    Rule = Code,
                    Message = $"Field '{shared.Field}' is read by bindings {string.Join(", ", shared.Bindings)}; a field has at most one binding, so its ID has one meaning."
                })
        ];
    }

    /// <summary>A field's key, or its ID when it is not a field of the version, which <c>binding_fields_in_version</c> reports.</summary>
    private static string FieldName(Dictionary<long, string> fieldKeys, long fieldId)
    {
        if (fieldKeys.TryGetValue(fieldId, out var key)) return key;
        return fieldId.ToString(CultureInfo.InvariantCulture);
    }
}
