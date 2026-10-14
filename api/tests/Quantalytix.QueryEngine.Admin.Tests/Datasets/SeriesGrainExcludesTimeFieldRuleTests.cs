using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms that <c>series_grain_excludes_time_field</c> reports a series grain that includes the source field of a time
/// binding of the version, and only that: row grains may include it, and fields bound by bindings without a time extension
/// are not observation times. A series identifies one trajectory over time (schema section 7); including the observation
/// time would make every observation its own series, and time-aware measures would select nothing meaningful.
/// </summary>
public sealed class SeriesGrainExcludesTimeFieldRuleTests
{
    /// <summary>The version being validated.</summary>
    private const long VersionId = 100;

    /// <summary>The version's fields: loanId, asOfDate (the time binding's field) and branchCode (a mapped binding's field).</summary>
    private readonly InMemoryTableGateway<FieldRecord> _fields = new(
        DatasetRows.ColumnField(1, VersionId, "loanId", "loan_id"),
        DatasetRows.ColumnField(2, VersionId, "asOfDate", "as_of_date", DataTypes.Date),
        DatasetRows.ColumnField(3, VersionId, "branchCode", "branch_code"));

    /// <summary>A time binding on asOfDate and a mapped binding on branchCode.</summary>
    private readonly InMemoryTableGateway<DimensionBindingRecord> _bindings = new(
        DatasetRows.Binding(21, VersionId, "As of", BindingKinds.Time, 2),
        DatasetRows.Binding(22, VersionId, "Branch", BindingKinds.Mapped, 3));

    /// <summary>The time extension of the As of binding only.</summary>
    private readonly InMemoryTableGateway<TimeBindingRecord> _timeBindings = new(DatasetRows.TimeBinding(21));

    /// <summary>A series grain holding the observation time is reported, naming the grain and the field.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task SeriesGrainWithTheTimeField_IsReported()
    {
        var violations = await EvaluateAsync(GrainKinds.Series, fieldIds: [1, 2]);

        var violation = Assert.Single(violations);
        Assert.Equal(SeriesGrainExcludesTimeFieldRule.Code, violation.Rule);
        Assert.Contains("'loanSeries'", violation.Message, StringComparison.Ordinal);
        Assert.Contains("'asOfDate'", violation.Message, StringComparison.Ordinal);
    }

    /// <summary>A row grain identifies single observations, so it may include the observation time.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task RowGrainWithTheTimeField_Passes()
    {
        Assert.Empty(await EvaluateAsync(GrainKinds.Row, fieldIds: [1, 2]));
    }

    /// <summary>A field bound without a time extension is not an observation time, so a series grain may include it.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task SeriesGrainWithANonTimeBoundField_Passes()
    {
        Assert.Empty(await EvaluateAsync(GrainKinds.Series, fieldIds: [1, 3]));
    }

    /// <summary>Evaluates the rule on a version holding one grain, <c>loanSeries</c>, of the given kind and fields.</summary>
    private Task<IReadOnlyList<RuleViolation>> EvaluateAsync(string grainKind, long[] fieldIds)
    {
        var grains = new InMemoryTableGateway<DatasetGrainRecord>(DatasetRows.Grain(11, VersionId, "loanSeries", grainKind));
        var grainFields = new InMemoryTableGateway<DatasetGrainFieldRecord>(
            fieldIds.Select((fieldId, index) => DatasetRows.GrainField(11, index + 1, fieldId)));
        var rule = new SeriesGrainExcludesTimeFieldRule(grains, grainFields, _bindings, _timeBindings, _fields);
        return rule.EvaluateAsync(VersionId, TestContext.Current.CancellationToken);
    }
}
