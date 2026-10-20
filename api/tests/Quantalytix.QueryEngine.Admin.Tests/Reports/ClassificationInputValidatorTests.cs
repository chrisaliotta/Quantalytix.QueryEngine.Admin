using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Mappings;
using Quantalytix.QueryEngine.Admin.Tests.Releases;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Reports;

/// <summary>
/// Confirms that a source's classification is checked against the report's dimension 500 when it is saved, because that dimension
/// never changes: a mapping set or bridge must lead into it, and a bridge starts from a role of the bridge's source dimension. Each
/// kind takes exactly its own columns. A classification into another dimension would classify every row into members the report
/// does not have, and a bridge from a role of another dimension would remap members the bridge does not know.
/// </summary>
public sealed class ClassificationInputValidatorTests
{
    /// <summary>The route of report version 61.</summary>
    private static readonly FixedRequestRoute VersionRoute = new(new Dictionary<string, long> { ["versionId"] = ReportVersionId });

    /// <summary>Mapping set 62 maps into the report's dimension, 63 into regions.</summary>
    private readonly InMemoryTableGateway<MappingSetRecord> _mappingSets = new(MappingRows.MappingSet(62, ReportDimensionId), MappingRows.MappingSet(63, 600));

    /// <summary>Bridge 95 leads from accounts into the report's dimension, 96 from accounts into regions.</summary>
    private readonly InMemoryTableGateway<BridgeRecord> _bridges = new(BridgeRows.Bridge(95, 800, ReportDimensionId), BridgeRows.Bridge(96, 800, 600));

    /// <summary>Role 85, glAccount, is a role of accounts (800); role 86, region, a role of regions (600).</summary>
    private readonly InMemoryTableGateway<DimensionRoleRecord> _roles = new(DimensionRows.Role(85, "glAccount", 800), DimensionRows.Role(86, "region", 600));

    /// <summary>A source mapped through mapping set 62, or bridged through bridge 95 from an account role, passes.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task ClassificationsIntoTheReportsDimension_Pass()
    {
        (await ValidateAsync(new ClassificationInput { Kind = ClassificationKinds.Mapped, FieldKey = "product", MappingSetId = 62 })).ShouldNotHaveAnyValidationErrors();
        (await ValidateAsync(new ClassificationInput { Kind = ClassificationKinds.Bridge, RoleId = 85, BridgeId = 95 })).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>A mapping set and a bridge into regions fail, and so does a direct classification that names a mapping set.</summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task ClassificationsIntoAnotherDimension_Fail()
    {
        var mapped = await ValidateAsync(new ClassificationInput { Kind = ClassificationKinds.Mapped, FieldKey = "product", MappingSetId = 63 });
        var bridged = await ValidateAsync(new ClassificationInput { Kind = ClassificationKinds.Bridge, RoleId = 85, BridgeId = 96 });
        var direct = await ValidateAsync(new ClassificationInput { Kind = ClassificationKinds.Direct, FieldKey = "member", MappingSetId = 62 });

        Assert.Equal("Mapping set 63 does not map into the report's dimension.", Assert.Single(mapped.Errors).ErrorMessage);
        Assert.Equal("Bridge 96 does not lead into the report's dimension.", Assert.Single(bridged.Errors).ErrorMessage);
        Assert.Equal("Only mapped classifications use 'Mapping Set Id', so it must be empty.", Assert.Single(direct.Errors).ErrorMessage);
    }

    /// <summary>
    /// A bridge reads its role's binding, so it starts from a role of the bridge's source dimension and names no field, and only a
    /// bridge names a role.
    /// </summary>
    /// <returns>A task that completes when the validation is checked.</returns>
    [Fact]
    public async Task BridgeClassifications_StartFromARoleOfTheBridgesSource()
    {
        var foreignRole = await ValidateAsync(new ClassificationInput { Kind = ClassificationKinds.Bridge, RoleId = 86, BridgeId = 95 });
        var withField = await ValidateAsync(new ClassificationInput { Kind = ClassificationKinds.Bridge, FieldKey = "account", RoleId = 85, BridgeId = 95 });
        var directRole = await ValidateAsync(new ClassificationInput { Kind = ClassificationKinds.Direct, FieldKey = "member", RoleId = 85 });

        Assert.Equal("Role 86 is no role of the source dimension of bridge 95.", Assert.Single(foreignRole.Errors).ErrorMessage);
        Assert.Equal("A bridge classification reads its role's binding, so 'Field Key' must be empty.", Assert.Single(withField.Errors).ErrorMessage);
        Assert.Equal("Only bridge classifications use 'Role Id', so it must be empty.", Assert.Single(directRole.Errors).ErrorMessage);
    }

    /// <summary>Validates a GL source of the balance variable with the classification.</summary>
    private Task<TestValidationResult<VariableSourceInput>> ValidateAsync(ClassificationInput classification)
    {
        var validator = new VariableSourceInputValidator(
            new VersionKeys(VersionRoute),
            new InMemoryTableGateway<VariableSourceRecord>(),
            new InMemoryTableGateway<DatasetRecord>(ReleaseRows.Dataset(1, "gl")),
            new InMemoryTableGateway<ReportVersionRecord>(ReportVersion(ReportVersionId)),
            new InMemoryTableGateway<ReportRecord>(Report()),
            _mappingSets,
            _bridges,
            _roles);
        var input = new VariableSourceInput
        {
            VariableKey = "balance", SourceKey = "gl_balance", DatasetId = 1, Classification = classification, TimeFieldKey = "as_of", MeasureKey = "balance", IsEnabled = true
        };
        return validator.TestValidateAsync(input, cancellationToken: TestContext.Current.CancellationToken);
    }
}
