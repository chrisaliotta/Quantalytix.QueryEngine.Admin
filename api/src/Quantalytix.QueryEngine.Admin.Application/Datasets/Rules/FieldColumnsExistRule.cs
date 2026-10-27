using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// <c>field_columns_exist</c>: every column a column or JSON path field reads is a column of the version's source object, as the
/// commit leaves it. A materialized column lives in the source object's materialized view, not in the object, so it is not checked.
/// </summary>
public sealed class FieldColumnsExistRule : DatasetRule
{
    /// <summary><c>field_columns_exist</c>.</summary>
    protected override string Code => "field_columns_exist";

    /// <summary>Reports every field whose source column the object lacks; StarRocks column names ignore case.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per such field.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        var columns = snapshot.SourceColumns.Select(column => column.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return snapshot.Fields
            .Where(field => field.SourceColumn is { } column && !columns.Contains(column))
            .OrderBy(field => field.FieldKey, StringComparer.Ordinal)
            .Select(field => Violation(
                DatasetTabs.Fields,
                $"Field '{field.FieldKey}' reads column '{field.SourceColumn}', which '{snapshot.Version.SourceObject}' does not have.",
                field.FieldId,
                field.FieldKey));
    }
}
