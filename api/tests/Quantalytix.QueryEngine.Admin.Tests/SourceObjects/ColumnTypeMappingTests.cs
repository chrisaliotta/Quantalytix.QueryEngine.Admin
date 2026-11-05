using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.SourceObjects;

/// <summary>
/// Confirms the mapping from StarRocks column types, spelled as information_schema writes them, to field types: character
/// types are strings, small integers int32, BIGINT int64, DECIMAL keeps its precision and scale, and FLOAT, DOUBLE, DATE,
/// DATETIME and JSON map to their namesakes, so floating-point columns are no longer declared decimal. BOOLEAN and LARGEINT have no names of their own
/// there: they read <c>tinyint(1)</c> and <c>bigint(20) unsigned</c>, and must still map to boolean and decimal(38,0)
/// rather than to the TINYINT and BIGINT types, as the live sandbox showed. Case and other display widths are ignored.
/// Every other type maps to nothing, so the pickers never offer a column the query engine cannot type. The columns
/// endpoint and bulk field creation both rely on this one mapping.
/// </summary>
public sealed class ColumnTypeMappingTests
{
    /// <summary>Each supported column type maps to its field type.</summary>
    /// <param name="columnType">The column type as information_schema writes it.</param>
    /// <param name="dataType">The expected data type.</param>
    /// <param name="precision">The expected decimal precision.</param>
    /// <param name="scale">The expected decimal scale.</param>
    [Theory]
    [InlineData("varchar(16)", DataTypes.String, null, null)]
    [InlineData("VARCHAR(65533)", DataTypes.String, null, null)]
    [InlineData("char(2)", DataTypes.String, null, null)]
    [InlineData("tinyint(1)", DataTypes.Boolean, null, null)]
    [InlineData("tinyint(4)", DataTypes.Int32, null, null)]
    [InlineData("smallint(6)", DataTypes.Int32, null, null)]
    [InlineData("int(11)", DataTypes.Int32, null, null)]
    [InlineData("bigint(20)", DataTypes.Int64, null, null)]
    [InlineData("bigint(20) unsigned", DataTypes.Decimal, 38, 0)]
    [InlineData("decimal(20,2)", DataTypes.Decimal, 20, 2)]
    [InlineData("DECIMAL(9, 6)", DataTypes.Decimal, 9, 6)]
    [InlineData("decimal128(38,10)", DataTypes.Decimal, 38, 10)]
    [InlineData("float", DataTypes.Float, null, null)]
    [InlineData("double", DataTypes.Double, null, null)]
    [InlineData("date", DataTypes.Date, null, null)]
    [InlineData("datetime", DataTypes.Datetime, null, null)]
    [InlineData("json", DataTypes.Json, null, null)]
    public void SupportedType_MapsToItsFieldType(string columnType, string dataType, int? precision, int? scale)
    {
        Assert.Equal(new FieldType { DataType = dataType, NumericPrecision = precision, NumericScale = scale }, ColumnTypeMapping.Map(columnType));
    }

    /// <summary>
    /// The column a managed table creates for a field type maps back to that type, so a field added to a managed table reads its
    /// own column; timestamp is the one exception, stored as datetime.
    /// </summary>
    /// <param name="dataType">The field's data type.</param>
    /// <param name="precision">The field's decimal precision.</param>
    /// <param name="scale">The field's decimal scale.</param>
    [Theory]
    [InlineData(DataTypes.Boolean, null, null)]
    [InlineData(DataTypes.Int32, null, null)]
    [InlineData(DataTypes.Int64, null, null)]
    [InlineData(DataTypes.Decimal, 18, 2)]
    [InlineData(DataTypes.Float, null, null)]
    [InlineData(DataTypes.Double, null, null)]
    [InlineData(DataTypes.String, null, null)]
    [InlineData(DataTypes.Date, null, null)]
    [InlineData(DataTypes.Datetime, null, null)]
    [InlineData(DataTypes.Json, null, null)]
    public void ManagedColumnType_MapsBackToItsFieldType(string dataType, int? precision, int? scale)
    {
        var type = new FieldType { DataType = dataType, NumericPrecision = precision, NumericScale = scale };

        Assert.Equal(type, ColumnTypeMapping.Map(ColumnTypeMapping.ColumnTypeOf(dataType, precision, scale)));
    }

    /// <summary>A type without a logical equivalent maps to nothing.</summary>
    /// <param name="columnType">The column type as information_schema writes it.</param>
    [Theory]
    [InlineData("hll")]
    [InlineData("bitmap")]
    [InlineData("percentile")]
    [InlineData("varbinary")]
    [InlineData("array<int(11)>")]
    [InlineData("map<varchar(16),int(11)>")]
    [InlineData("struct<code varchar(4)>")]
    public void UnsupportedType_MapsToNothing(string columnType)
    {
        Assert.Null(ColumnTypeMapping.Map(columnType));
    }
}
