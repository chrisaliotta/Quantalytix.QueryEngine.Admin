using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms the generated-column DDL for every data type: the column type, and the CAST around <c>get_json_string</c>,
/// match the cast table of the query engine's FieldSql, and strings stay uncast text. The engine reads a set materialized
/// column instead of extracting the JSON value, so any difference from FieldSql would change query results.
/// </summary>
public sealed class MaterializedColumnDdlTests
{
    /// <summary>Each data type gets FieldSql's column type and cast; datetime and timestamp both become DATETIME.</summary>
    /// <param name="dataType">The field's data type.</param>
    /// <param name="precision">The field's decimal precision.</param>
    /// <param name="scale">The field's decimal scale.</param>
    /// <param name="definition">The expected column definition after the column name.</param>
    [Theory]
    [InlineData(DataTypes.String, null, null, "VARCHAR(65533) NULL AS get_json_string(`attributes`, '$.region.code')")]
    [InlineData(DataTypes.Boolean, null, null, "BOOLEAN NULL AS CAST(get_json_string(`attributes`, '$.region.code') AS BOOLEAN)")]
    [InlineData(DataTypes.Int32, null, null, "INT NULL AS CAST(get_json_string(`attributes`, '$.region.code') AS INT)")]
    [InlineData(DataTypes.Int64, null, null, "BIGINT NULL AS CAST(get_json_string(`attributes`, '$.region.code') AS BIGINT)")]
    [InlineData(DataTypes.Decimal, 18, 4, "DECIMAL(18, 4) NULL AS CAST(get_json_string(`attributes`, '$.region.code') AS DECIMAL(18, 4))")]
    [InlineData(DataTypes.Date, null, null, "DATE NULL AS CAST(get_json_string(`attributes`, '$.region.code') AS DATE)")]
    [InlineData(DataTypes.Json, null, null, "JSON NULL AS CAST(get_json_string(`attributes`, '$.region.code') AS JSON)")]
    [InlineData(DataTypes.Datetime, null, null, "DATETIME NULL AS CAST(get_json_string(`attributes`, '$.region.code') AS DATETIME)")]
    [InlineData(DataTypes.Timestamp, null, null, "DATETIME NULL AS CAST(get_json_string(`attributes`, '$.region.code') AS DATETIME)")]
    public void AddColumn_MatchesFieldSqlForEachDataType(string dataType, int? precision, int? scale, string definition)
    {
        var field = DatasetRows.ColumnField(1, 100, "regionCode", "attributes", dataType) with
        {
            FieldSourceKind = FieldSourceKinds.JsonPath,
            JsonPath = "$.region.code",
            NumericPrecision = precision,
            NumericScale = scale
        };

        var statement = MaterializedColumnDdl.AddColumn("loan_facts", "region_code", field);

        Assert.Equal($"ALTER TABLE `loan_facts` ADD COLUMN `region_code` {definition}", statement);
    }
}
