using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.MaterializedViews;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SchemaChanges;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.SourceObjects;

/// <summary>
/// Confirms the StarRocks DDL text of managed tables (OI-33) and JSON field materialized views (OI-32), written after StarRocks'
/// documented syntax and to be verified against a live cluster: identifiers are quoted, a managed table always has its load
/// timestamp as the key column, a view reads every column of its object plus one cast extraction per field, and the views' refresh
/// status maps onto the contract's states.
/// </summary>
public sealed class StarRocksDdlTests
{
    /// <summary>A new managed table without columns still has a key column, which StarRocks needs.</summary>
    [Fact]
    public void ManagedTable_StartsWithItsLoadTimestamp()
    {
        Assert.Equal(
            "CREATE TABLE `budget` (`qe_loaded_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP) DUPLICATE KEY(`qe_loaded_at`) DISTRIBUTED BY RANDOM",
            ManagedTableSql.Create("budget", []));
    }

    /// <summary>New columns are typed like the fields that read them and added in one schema change.</summary>
    [Fact]
    public void AddColumns_TypesEachColumnLikeItsField()
    {
        DatasetNewColumnInput[] columns =
        [
            new() { Name = "amount", DataType = DataTypes.Decimal, NumericPrecision = 18, NumericScale = 2, IsNullable = false },
            new() { Name = "note", DataType = DataTypes.String, NumericPrecision = null, NumericScale = null, IsNullable = true }
        ];

        Assert.Equal("ALTER TABLE `budget` ADD COLUMN (`amount` DECIMAL(18, 2) NOT NULL, `note` VARCHAR(65533) NULL)", ManagedTableSql.AddColumns("budget", columns));
    }

    /// <summary>A view reads the object's columns and each extraction: strings as text, JSON as JSON, other types cast.</summary>
    [Fact]
    public void MaterializedView_ReadsTheObjectAndEachExtraction()
    {
        var definition = new MaterializedViewDefinition
        {
            ViewName = "qe_mv_loans",
            SourceObject = "loans",
            Columns =
            [
                Column("attributes_risk_string", DataTypes.String),
                Column("attributes_risk_int32", DataTypes.Int32),
                Column("attributes_risk_json", DataTypes.Json)
            ]
        };

        Assert.Equal(
            "CREATE MATERIALIZED VIEW `qe_mv_loans` REFRESH ASYNC AS SELECT `loans`.*, " +
            "get_json_string(`loans`.`attributes`, '$.risk') AS `attributes_risk_string`, " +
            "CAST(get_json_string(`loans`.`attributes`, '$.risk') AS INT) AS `attributes_risk_int32`, " +
            "json_query(`loans`.`attributes`, '$.risk') AS `attributes_risk_json` FROM `loans`",
            MaterializedViewSql.Create(definition));
    }

    /// <summary>An inactive view or a failed refresh is failed with StarRocks' reason; a success is ready; anything else is building.</summary>
    /// <param name="isActive">The <c>is_active</c> value.</param>
    /// <param name="refreshState">The <c>last_refresh_state</c> value.</param>
    /// <param name="state">The expected state.</param>
    [Theory]
    [InlineData("false", "SUCCESS", MaterializationStates.Failed)]
    [InlineData("true", "FAILED", MaterializationStates.Failed)]
    [InlineData("true", "SUCCESS", MaterializationStates.Ready)]
    [InlineData("true", "RUNNING", MaterializationStates.Building)]
    [InlineData("true", "", MaterializationStates.Building)]
    public void RefreshStatus_MapsOntoTheContractStates(string isActive, string refreshState, string state)
    {
        var view = new InformationSchemaMaterializedView
        {
            TableName = "qe_mv_loans",
            IsActive = isActive,
            InactiveReason = "base table dropped",
            LastRefreshState = refreshState,
            LastRefreshFinishedTime = "2026-10-27 12:00:00",
            LastRefreshErrorMessage = "cast failed"
        };

        Assert.Equal(state, StarRocksMaterializedViews.StatusOf(view, DateTime.UnixEpoch).State);
    }

    /// <summary>A view column extracting <c>$.risk</c> from <c>attributes</c>.</summary>
    private static MaterializedViewColumn Column(string name, string dataType)
    {
        return new MaterializedViewColumn { Name = name, SourceColumn = "attributes", JsonPath = "$.risk", DataType = dataType, NumericPrecision = null, NumericScale = null };
    }
}
