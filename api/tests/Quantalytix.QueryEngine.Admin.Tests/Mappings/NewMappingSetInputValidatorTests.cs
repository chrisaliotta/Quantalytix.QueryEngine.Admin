using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Mappings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Releases;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Mappings;

/// <summary>
/// Confirms that no mapping set targets a report dimension. A report's rows are reached through its own sources only, by bridge or
/// by field, so a mapping set into them would classify rows the report editor does not know about and would block its edits.
/// </summary>
public sealed class NewMappingSetInputValidatorTests
{
    /// <summary>The analytical branch dimension and report dimension 500.</summary>
    private readonly InMemoryTableGateway<DimensionRecord> _dimensions = new(
        ReleaseRows.Dimension(100, "branch"),
        ReleaseRows.Dimension(500, "balance_sheet") with { DimensionPurpose = DimensionPurposes.Report });

    /// <summary>A mapping set into the branch dimension passes, and one into the report dimension fails.</summary>
    /// <returns>A task that completes when the inputs are validated.</returns>
    [Fact]
    public async Task MappingSetIntoAReportDimension_IsRefused()
    {
        var validator = new NewMappingSetInputValidator(new InMemoryTableGateway<MappingSetRecord>(), _dimensions);
        var cancellationToken = TestContext.Current.CancellationToken;

        var passes = await validator.TestValidateAsync(Input(100), cancellationToken: cancellationToken);
        var refused = await validator.TestValidateAsync(Input(500), cancellationToken: cancellationToken);

        passes.ShouldNotHaveAnyValidationErrors();
        refused.ShouldHaveValidationErrorFor(input => input.DimensionId)
            .WithErrorMessage("Dimension 500 holds a report's rows; its report's sources map into it by bridge or by field.");
    }

    /// <summary>A new mapping set of core banking codes into a dimension.</summary>
    private static NewMappingSetInput Input(long dimensionId)
    {
        return new NewMappingSetInput { MappingSetKey = "core_codes", SourceNamespace = "core", DimensionId = dimensionId, DisplayName = "Core codes" };
    }
}
