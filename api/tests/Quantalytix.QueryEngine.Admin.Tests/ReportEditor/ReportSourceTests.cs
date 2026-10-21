using FluentValidation.TestHelper;
using Quantalytix.QueryEngine.Admin.Application.ReportEditor;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.ReportEditor.ReportEditorFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.ReportEditor;

/// <summary>
/// Confirms how report sources reach the report's rows. A bridge source gets a bridge of its own, published at once and pinned in the
/// active set, so its rules are live, and many-to-many and replicating, so an item can add into several rows; a copy of a sibling's
/// rules is a snapshot that later edits of the sibling do not reach, because each source's rules are its own; and deleting the
/// source deletes its bridge with its version, entries and pin, which nothing else uses. A direct source needs an enabled field of
/// the active release's dataset version, whose values the engine reads as row keys.
/// </summary>
public sealed class ReportSourceTests
{
    /// <summary>A bridge source copying <c>gl</c> owns a published, pinned bridge with a snapshot of its entries, which goes with the source.</summary>
    /// <returns>A task that completes when the source's lifecycle is checked.</returns>
    [Fact]
    public async Task BridgeSource_OwnsAPublishedBridgeThatGoesWithIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new ReportEditorFixture();
        var input = new NewReportSourceInput
        {
            SourceKey = "gl_copy",
            DisplayName = "GL copy",
            DatasetId = GlDatasetId,
            MappingMethod = ReportMappingMethods.Bridge,
            RoleId = AccountRoleId,
            TimeFieldKey = "as_of_date",
            CopyFromSourceId = SourceId
        };

        Assert.Empty(await fixture.SourceService().CreateAsync(ReportRows.ReportId, input, cancellationToken));

        var source = Assert.Single(fixture.Sources.All, source => source.SourceKey == "gl_copy");
        var bridge = Assert.Single(fixture.Bridges.All, bridge => bridge.BridgeId == source.BridgeId);
        Assert.Equal(("balance_sheet_gl_copy", AccountDimensionId, ReportRows.ReportDimensionId), (bridge.BridgeKey, bridge.SourceDimensionId, bridge.TargetDimensionId));
        var version = Assert.Single(fixture.BridgeVersions.All, version => version.BridgeId == bridge.BridgeId);
        Assert.Equal(PublicationStatuses.Published, version.Status);
        Assert.Equal((Cardinalities.ManyToMany, AllocationModes.Replicate), (version.Cardinality, version.AllocationMode));
        Assert.Contains(fixture.BridgePins.All, pin => pin.BridgeId == bridge.BridgeId && pin.BridgeVersionId == version.BridgeVersionId);
        Assert.Equal([("1001", "LOANS"), ("1002", "CASH")], Entries(fixture, version.BridgeVersionId));

        await fixture.Items().ReplaceAsync(ReportRows.ReportId, SourceId, new SourceItemsInput { RowKey = "LOANS", Items = [Item("1001"), Item("1003")] }, cancellationToken);
        Assert.Equal([("1001", "LOANS"), ("1002", "CASH")], Entries(fixture, version.BridgeVersionId));

        Assert.Empty(await fixture.SourceService().DeleteAsync(ReportRows.ReportId, source.ReportSourceId, cancellationToken));
        Assert.DoesNotContain(fixture.Bridges.All, other => other.BridgeId == bridge.BridgeId);
        Assert.DoesNotContain(fixture.BridgeVersions.All, other => other.BridgeId == bridge.BridgeId);
        Assert.Empty(Entries(fixture, version.BridgeVersionId));
        Assert.DoesNotContain(fixture.BridgePins.All, pin => pin.BridgeId == bridge.BridgeId);
    }

    /// <summary>A direct source by the enabled account code passes, and one by the disabled memo field fails.</summary>
    /// <returns>A task that completes when the inputs are validated.</returns>
    [Fact]
    public async Task DirectSource_NeedsAnEnabledField()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var validator = new ReportEditorFixture().NewSourceValidator();

        var passes = await validator.TestValidateAsync(Direct("gl_account"), cancellationToken: cancellationToken);
        var refused = await validator.TestValidateAsync(Direct("memo"), cancellationToken: cancellationToken);

        passes.ShouldNotHaveAnyValidationErrors();
        refused.ShouldHaveValidationErrorFor(input => input.FieldKey)
            .WithErrorMessage("Field 'memo' is no enabled field of the active release's version of the dataset.");
    }

    /// <summary>A direct source of GL that reads row keys from a field.</summary>
    private static NewReportSourceInput Direct(string fieldKey)
    {
        return new NewReportSourceInput
        {
            SourceKey = "gl_direct",
            DisplayName = "GL direct",
            DatasetId = GlDatasetId,
            MappingMethod = ReportMappingMethods.Direct,
            FieldKey = fieldKey,
            TimeFieldKey = "as_of_date"
        };
    }

    /// <summary>A rule taking one item alone, whole.</summary>
    private static SourceItemInput Item(string itemKey)
    {
        return new SourceItemInput { ItemKey = itemKey, Selection = SourceSelections.Member, Factor = 1m };
    }

    /// <summary>A bridge version's entries as (item, row) pairs, by item.</summary>
    private static (string, string)[] Entries(ReportEditorFixture fixture, long bridgeVersionId)
    {
        return
        [
            .. fixture.BridgeMappings.All.Where(mapping => mapping.BridgeVersionId == bridgeVersionId)
                .Select(mapping => (mapping.SourceMemberKey, mapping.TargetMemberKey))
                .Order()
        ];
    }
}
