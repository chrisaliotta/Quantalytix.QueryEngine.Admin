using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary><c>field_key_unique</c>: no two fields of a version share a key, because requests name fields by key (D4).</summary>
public sealed class FieldKeyUniqueRule : DatasetRule
{
    /// <summary><c>field_key_unique</c>.</summary>
    protected override string Code => "field_key_unique";

    /// <summary>Reports every key more than one field uses, at its second field.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per duplicated key.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        return snapshot.Fields
            .GroupBy(field => field.FieldKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => Violation(DatasetTabs.Fields, $"Field key '{group.Key}' is used by {group.Count()} fields of this version.", group.ElementAt(1).FieldId, group.Key));
    }
}
