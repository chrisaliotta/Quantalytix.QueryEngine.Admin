using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.ReportEditor;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.ReportEditor.ReportEditorFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.ReportEditor;

/// <summary>
/// Confirms a source's override of its variables' missing behavior. A budget source has no rows for the lines and months it does not
/// plan, which must read null rather than the zero an actuals variable defaults to, so the override is stored with the variable for
/// that source alone; it is a missing behavior the engine knows, or null to keep the variable's.
/// </summary>
public sealed class SourceVariableTests
{
    /// <summary>The seeded editor tables: source <c>gl</c> supplies <c>balance</c>, whose missing behavior is zero.</summary>
    private readonly ReportEditorFixture _fixture = new();

    /// <summary>A known override, or none, passes; another value fails on its variable.</summary>
    /// <param name="missingBehavior">The override sent.</param>
    /// <param name="valid">Whether it passes.</param>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Theory]
    [InlineData(VariableMissingBehaviors.Null, true)]
    [InlineData(null, true)]
    [InlineData("skip", false)]
    public async Task MissingBehavior_IsKnownOrNone(string? missingBehavior, bool valid)
    {
        var result = await _fixture.VariablesValidator().TestValidateAsync(Input(missingBehavior), cancellationToken: TestContext.Current.CancellationToken);

        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveValidationErrorFor("Variables[0].MissingBehavior");
    }

    /// <summary>The override is stored with the source's variable, where the editor view and the engine read it.</summary>
    /// <returns>A task that completes when the write is checked.</returns>
    [Fact]
    public async Task MissingBehavior_IsStoredWithTheVariable()
    {
        var violations = await _fixture.SourceVariableService().ReplaceAsync(ReportRows.ReportId, SourceId, Input(VariableMissingBehaviors.Null), TestContext.Current.CancellationToken);

        Assert.Empty(violations);
        Assert.Equal(VariableMissingBehaviors.Null, Assert.Single(_fixture.SourceVariables.All).MissingBehavior);
    }

    /// <summary>The source's one variable, balance from the balance measure, with the given override.</summary>
    private static SourceVariablesInput Input(string? missingBehavior)
    {
        return new SourceVariablesInput
        {
            Variables = [new SourceVariableInput { VariableKey = "balance", MeasureKey = "balance", MissingBehavior = missingBehavior }]
        };
    }
}
