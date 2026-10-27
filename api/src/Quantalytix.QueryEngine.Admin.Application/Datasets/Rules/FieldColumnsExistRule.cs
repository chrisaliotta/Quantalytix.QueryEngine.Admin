using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// Requires every source column and materialized column of a dataset version's fields to be a column of the version's
/// source object, because the query engine reads them from it by name. StarRocks column names ignore case.
/// </summary>
public sealed class FieldColumnsExistRule(
    ITableGateway<DatasetVersionRecord> versions,
    ITableGateway<FieldRecord> fields,
    SourceObjectService sourceObjects) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "field_columns_exist";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each source or materialized column that the source object does not have.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per missing column.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var columns = await sourceObjects.ListColumnsAsync(version.SourceObject, cancellationToken);
        var columnNames = columns.Select(column => column.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = await fields.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var violations = new List<RuleViolation>();
        foreach (var field in rows.OrderBy(row => row.FieldKey, StringComparer.Ordinal))
        {
            if (field.SourceColumn is { } sourceColumn && !columnNames.Contains(sourceColumn))
                violations.Add(Violation($"Field '{field.FieldKey}' reads column '{sourceColumn}', which '{version.SourceObject}' does not have."));
            if (field.MaterializedColumn is { } materializedColumn && !columnNames.Contains(materializedColumn))
                violations.Add(Violation($"Field '{field.FieldKey}' has materialized column '{materializedColumn}', which '{version.SourceObject}' does not have."));
        }

        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Code = Code, Message = message };
    }
}
