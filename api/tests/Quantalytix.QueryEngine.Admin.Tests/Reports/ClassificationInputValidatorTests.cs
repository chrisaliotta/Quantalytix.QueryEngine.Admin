using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.Mappings;
using Quantalytix.QueryEngine.Admin.Tests.Releases;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms that a classification is checked against the dimension it targets when it is saved, because that dimension never
/// changes: a source's mapping set or bridge must lead into the report's dimension 500, and a route's into its calculation dimension's
/// dimension, region 600, or be a field route for the scalar <c>channel</c>. Each kind takes exactly its own columns. A classification
/// into another dimension would classify every row into members the report does not have.
/// </summary>
public sealed class ClassificationInputValidatorTests
{
    /// <summary>The route of report version 61.</summary>
    private static readonly FixedRequestRoute VersionRoute = new(new Dictionary<string, long> { ["versionId"] = ReportVersionId });

    /// <summary>Mapping set 62 maps into the report's dimension, 63 into regions.</summary>
    private readonly InMemoryTableGateway<MappingSetRecord> _mappingSets = new(MappingRows.MappingSet(62, ReportDimensionId), MappingRows.MappingSet(63, 600));

    /// <summary>Bridge 95 leads from accounts into the report's dimension, 96 from accounts into regions.</summary>
    private readonly InMemoryTableGateway<BridgeRecord> _bridges = new(BridgeRows.Bridge(95, 800, ReportDimensionId), BridgeRows.Bridge(96, 800, 600));

    /// <summary>A source classified through mapping set 62 or bridge 95 passes.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task SourceClassificationsIntoTheReportsDimension_Pass()
    {
        (await ValidateSourceAsync(new ClassificationInput { Kind = ClassificationKinds.Mapped, FieldKey = "product", MappingSetId = 62 })).ShouldNotHaveAnyValidationErrors();
        (await ValidateSourceAsync(new ClassificationInput { Kind = ClassificationKinds.Bridge, FieldKey = "account", BridgeId = 95 })).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A mapping set and a bridge into regions fail, and so does a direct classification that names a mapping set.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task SourceClassificationsIntoAnotherDimension_Fail()
    {
        var mapped = await ValidateSourceAsync(new ClassificationInput { Kind = ClassificationKinds.Mapped, FieldKey = "product", MappingSetId = 63 });
        var bridged = await ValidateSourceAsync(new ClassificationInput { Kind = ClassificationKinds.Bridge, FieldKey = "account", BridgeId = 96 });
        var direct = await ValidateSourceAsync(new ClassificationInput { Kind = ClassificationKinds.Direct, FieldKey = "member", MappingSetId = 62 });

        Assert.Equal("Mapping set 63 does not map into the dimension this classification targets.", Assert.Single(mapped.Errors).ErrorMessage);
        Assert.Equal("Bridge 96 does not lead into the dimension this classification targets.", Assert.Single(bridged.Errors).ErrorMessage);
        Assert.Equal("Only mapped classifications use 'Mapping Set Id', so it must be empty.", Assert.Single(direct.Errors).ErrorMessage);
    }

    /// <summary>
    /// A route of region through mapping set 63 passes and one through 62 fails; the scalar channel takes a field route and refuses
    /// a direct one, and region refuses a field route.
    /// </summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task RouteClassifications_FollowTheirCalculationDimension()
    {
        (await ValidateRouteAsync(81, new ClassificationInput { Kind = ClassificationKinds.Mapped, FieldKey = "branch", MappingSetId = 63 })).ShouldNotHaveAnyValidationErrors();
        (await ValidateRouteAsync(82, new ClassificationInput { Kind = RouteKinds.Field, FieldKey = "channel" })).ShouldNotHaveAnyValidationErrors();

        var wrongSet = await ValidateRouteAsync(81, new ClassificationInput { Kind = ClassificationKinds.Mapped, FieldKey = "branch", MappingSetId = 62 });
        var scalarDirect = await ValidateRouteAsync(82, new ClassificationInput { Kind = ClassificationKinds.Direct, FieldKey = "channel" });
        var canonicalField = await ValidateRouteAsync(81, new ClassificationInput { Kind = RouteKinds.Field, FieldKey = "branch" });

        Assert.Equal("Mapping set 62 does not map into the dimension this classification targets.", Assert.Single(wrongSet.Errors).ErrorMessage);
        Assert.Equal("A scalar calculation dimension reads raw values, so its routes are field routes.", Assert.Single(scalarDirect.Errors).ErrorMessage);
        Assert.Equal("A field route reads raw values, so only a scalar calculation dimension takes one.", Assert.Single(canonicalField.Errors).ErrorMessage);
    }

    /// <summary>Validates a GL source of the balance variable with the classification.</summary>
    private Task<TestValidationResult<VariableSourceInput>> ValidateSourceAsync(ClassificationInput classification)
    {
        var validator = new VariableSourceInputValidator(
            new VersionKeys(VersionRoute),
            new InMemoryTableGateway<VariableSourceRecord>(),
            new InMemoryTableGateway<DatasetRecord>(ReleaseRows.Dataset(1, "gl")),
            new InMemoryTableGateway<ReportVersionRecord>(ReportVersion(ReportVersionId)),
            new InMemoryTableGateway<ReportRecord>(Report()),
            _mappingSets,
            _bridges);
        var input = new VariableSourceInput
        {
            VariableKey = "balance", SourceKey = "gl_balance", DatasetId = 1, Classification = classification, TimeFieldKey = "as_of", MeasureKey = "balance", IsEnabled = true
        };
        return validator.TestValidateAsync(input, cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>Validates a GL route of calculation dimension 81, region over dimension 600, or 82, the scalar channel.</summary>
    private Task<TestValidationResult<CalculationRouteInput>> ValidateRouteAsync(long calculationDimensionId, ClassificationInput classification)
    {
        var validator = new CalculationRouteInputValidator(
            new VersionKeys(VersionRoute),
            new InMemoryTableGateway<CalculationDimensionRecord>(
                CalculationDimension(81, "region", 600),
                CalculationDimension(82, "channel", 600) with
                {
                    ValueKind = CalculationValueKinds.Scalar, DimensionId = null, GroupingMode = null, ValueDataType = DataTypes.String
                }),
            new InMemoryTableGateway<VariableSourceRecord>(),
            new InMemoryTableGateway<DatasetRecord>(ReleaseRows.Dataset(1, "gl")),
            new InMemoryTableGateway<CalculationRouteRecord>(),
            _mappingSets,
            _bridges);
        var input = new CalculationRouteInput { CalculationDimensionId = calculationDimensionId, DatasetId = 1, Classification = classification };
        return validator.TestValidateAsync(input, cancellationToken: TestContext.Current.CancellationToken);
    }
}
