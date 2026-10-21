using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms <c>profiles_complete</c>: an enabled profile of a report is reported for each variable the report reads, through its
/// columns and formula rows and the formulas of its model, that it picks no source for or picks a source for that does not supply
/// it; a variable the report never reads and a disabled profile are not checked. A run reads every variable it needs through the
/// profile's pick, so a gap would leave the variable without data.
/// </summary>
public sealed class ProfilesCompleteRuleTests
{
    /// <summary>The seeded tables, with variables income and expense, yield reading income over balance, and a yield column.</summary>
    private readonly ReportGraphFixture _fixture = new();

    /// <summary>
    /// The yield column reads income through its formula: <c>base</c> picks a source for it that does not supply it and
    /// <c>budget</c> none, while expense, which nothing reads, needs no pick and the disabled <c>legacy</c> picks nothing at all.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task VariablesReadThroughFormulas_NeedAPickOfASupplyingSource()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedYieldAsync();
        await _fixture.Picks.InsertAsync(Pick(75, "income", GlSourceId), cancellationToken);
        await _fixture.Profiles.InsertAsync(Profile(76, "budget"), cancellationToken);
        await _fixture.Picks.InsertAsync(Pick(76, "balance", GlSourceId), cancellationToken);
        await _fixture.Profiles.InsertAsync(Profile(77, "legacy") with { IsEnabled = false }, cancellationToken);

        var violations = await _fixture.EvaluateAsync(new ProfilesCompleteRule());

        Assert.Equal(
            ["Report 'balance_sheet' profile 'base' picks a source that does not supply variable 'income'.",
             "Report 'balance_sheet' profile 'budget' picks no source for variable 'income'."],
            violations.Select(violation => violation.Message));
    }

    /// <summary>Adds variables income and expense, makes yield read income over balance, and adds a column that returns yield.</summary>
    private async Task SeedYieldAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Variables.InsertAsync(Variable(52, "income"), cancellationToken);
        await _fixture.Variables.InsertAsync(Variable(53, "expense"), cancellationToken);
        await _fixture.CalculatedMeasures.InsertAsync(Calculated(61, "yield", "[m.income] / [m.balance]"), cancellationToken);
        await _fixture.Columns.InsertAsync(Column(2, "yield", ReportValueKinds.Calculated, "yield", 2), cancellationToken);
    }
}
