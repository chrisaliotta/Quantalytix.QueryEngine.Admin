using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms that <c>series_grain_excludes_time_field</c> reports a series grain that includes a time binding's observation-time
/// field, because a series is the set of rows that differ only in time; row grains and fields bound without a time axis may.
/// </summary>
public sealed class SeriesGrainExcludesTimeFieldRuleTests
{
    /// <summary>A series grain holding the observation time is reported, naming the grain and the field.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task SeriesGrainWithTheTimeField_IsReported()
    {
        var violation = Assert.Single(await EvaluateAsync(GrainKinds.Series, fieldIds: [1, 2]));

        Assert.Equal("series_grain_excludes_time_field", violation.Code);
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

    /// <summary>A field bound without a time axis is not an observation time, so a series grain may include it.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task SeriesGrainWithANonTimeBoundField_Passes()
    {
        Assert.Empty(await EvaluateAsync(GrainKinds.Series, fieldIds: [1, 3]));
    }

    /// <summary>Evaluates the rule on a version holding one grain, <c>loanSeries</c>, of the given kind and fields.</summary>
    private static Task<IReadOnlyList<RuleViolation>> EvaluateAsync(string grainKind, long[] fieldIds)
    {
        var snapshot = DatasetRows.Snapshot(
            fields:
            [
                DatasetRows.ColumnField(1, 100, "loanId", "loan_id"),
                DatasetRows.ColumnField(2, 100, "asOfDate", "as_of_date", DataTypes.Date),
                DatasetRows.ColumnField(3, 100, "branchCode", "branch_code")
            ],
            grains: [DatasetRows.Grain(11, 100, "loanSeries", grainKind)],
            grainFields: [.. fieldIds.Select((fieldId, index) => DatasetRows.GrainField(11, index + 1, fieldId))],
            bindings: [DatasetRows.Binding(21, 100, null, BindingKinds.Time, 2), DatasetRows.Binding(22, 100, 81, BindingKinds.Mapped, 3)],
            timeBindings: [DatasetRows.TimeBinding(21)],
            roles: [DimensionRows.Role(81, "branch", 7000)]);
        return new SeriesGrainExcludesTimeFieldRule().EvaluateAsync(snapshot, TestContext.Current.CancellationToken);
    }
}
