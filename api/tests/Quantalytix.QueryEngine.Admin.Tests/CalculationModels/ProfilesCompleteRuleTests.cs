using Quantalytix.QueryEngine.Admin.Application.CalculationModels;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.CalculationModels.ModelRows;

namespace Quantalytix.QueryEngine.Admin.Tests.CalculationModels;

/// <summary>
/// Confirms <c>profiles_complete</c>: an enabled profile is reported for each variable it picks no source for, and for each
/// pick of a disabled source; a disabled profile is not checked. A report runs under one profile and reads every variable
/// through that profile's pick, so a gap would leave the variable without data in every report run under the profile.
/// </summary>
public sealed class ProfilesCompleteRuleTests
{
    /// <summary>
    /// <c>actual</c> picks a disabled source for income, <c>budget</c> picks nothing for income, and the disabled <c>legacy</c>
    /// picks nothing at all.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task MissingAndDisabledPicks_AreReported()
    {
        var rule = new ProfilesCompleteRule(
            new InMemoryTableGateway<VariableRecord>(Variable(51, "balance"), Variable(52, "income")),
            new InMemoryTableGateway<VariableSourceRecord>(Source(71, 51, "gl_balance", 1), Source(72, 52, "gl_income", 1) with { IsEnabled = false }),
            new InMemoryTableGateway<SourceProfileRecord>(Profile(81, "actual"), Profile(82, "budget"), Profile(83, "legacy") with { IsEnabled = false }),
            new InMemoryTableGateway<SourceProfileVariableRecord>(Pick(81, 51, 71), Pick(81, 52, 72), Pick(82, 51, 71)));

        var violations = await rule.EvaluateAsync(ModelVersionId, TestContext.Current.CancellationToken);

        Assert.Equal(
            ["Profile 'actual' picks a disabled source for variable 'income'.", "Profile 'budget' picks no source for variable 'income'."],
            violations.Select(violation => violation.Message));
    }
}
