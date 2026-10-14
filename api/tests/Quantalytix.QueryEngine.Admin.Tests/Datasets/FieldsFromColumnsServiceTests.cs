using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.SourceObjects;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms how fields are created from physical columns: one <c>column</c> field per selected, typed column in column
/// order, keyed in camelCase and named in Title Case, typed by the column type mapping, groupable only for string, boolean,
/// integer and date types; columns without a logical type, unselected columns and columns that a <c>column</c> field
/// already reads are skipped. Bulk creation is how wide tables get their fields, so its naming and defaults decide the
/// request-facing IDs.
/// </summary>
public sealed class FieldsFromColumnsServiceTests
{
    /// <summary>The draft version over <c>loan_daily</c>.</summary>
    private const long VersionId = 100;

    /// <summary>The first ID the service hands out.</summary>
    private const long FirstNewId = 1_000_000;

    /// <summary>The columns of <c>loan_daily</c>, in ordinal order.</summary>
    private static readonly PhysicalColumn[] Columns =
    [
        Column("loan_id", "varchar(16)"),
        Column("as_of_date", "date"),
        Column("branch_code", "varchar(16)"),
        Column("is_active", "boolean"),
        Column("interest_rate", "decimal(9,6)"),
        Column("risk_score", "double"),
        Column("attributes", "json"),
        Column("visitor_sketch", "hll")
    ];

    /// <summary>
    /// Selected typed columns become fields in column order; the column a <c>column</c> field already reads is skipped, but a
    /// JSON column that only a <c>json_path</c> field reads is not, and an untyped column is never created.
    /// </summary>
    /// <returns>A task that completes when the fields are checked.</returns>
    [Fact]
    public async Task SelectedTypedUnusedColumns_BecomeNamedTypedFields()
    {
        var fields = new InMemoryTableGateway<FieldRecord>(
            DatasetRows.ColumnField(1, VersionId, "loan", "loan_id"),
            DatasetRows.ColumnField(2, VersionId, "region", "attributes") with { FieldSourceKind = FieldSourceKinds.JsonPath, JsonPath = "$.region" });
        var input = new FieldsFromColumnsInput
        {
            Columns = ["loan_id", "as_of_date", "is_active", "interest_rate", "risk_score", "attributes", "visitor_sketch"]
        };

        var created = await Service(fields).CreateAsync(VersionId, input, TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                ("asOfDate", "As Of Date", "as_of_date", DataTypes.Date, (int?)null, (int?)null, true),
                ("isActive", "Is Active", "is_active", DataTypes.Boolean, null, null, true),
                ("interestRate", "Interest Rate", "interest_rate", DataTypes.Decimal, 9, 6, false),
                ("riskScore", "Risk Score", "risk_score", DataTypes.Decimal, 38, 12, false),
                ("attributes", "Attributes", "attributes", DataTypes.Json, null, null, false)
            ],
            created.Select(field => (field.FieldKey, field.DisplayName, field.SourceColumn, field.DataType, field.NumericPrecision, field.NumericScale, field.IsGroupable)));
        Assert.All(created, field =>
        {
            Assert.True(field.FieldId >= FirstNewId);
            Assert.Equal(VersionId, field.DatasetVersionId);
            Assert.Equal(FieldSourceKinds.Column, field.FieldSourceKind);
            Assert.True(field.IsNullable && field.IsSelectable && field.IsFilterable && field.IsSortable && field.IsEnabled);
        });
        Assert.Equal(created, fields.All.Where(field => field.FieldId >= FirstNewId));
    }

    /// <summary>The service over the given fields and a catalog holding <c>loan_daily</c>.</summary>
    private static FieldsFromColumnsService Service(InMemoryTableGateway<FieldRecord> fields)
    {
        var versions = new InMemoryTableGateway<DatasetVersionRecord>(DatasetRows.Version(VersionId));
        var catalog = new FixedSourceObjectCatalog(
            [new SourceObject { Name = "loan_daily", Kind = SourceObjectKinds.Table }],
            new Dictionary<string, IReadOnlyList<PhysicalColumn>> { ["loan_daily"] = Columns });
        return new FieldsFromColumnsService(fields, versions, new SourceObjectService(catalog), new SequentialIdGenerator(FirstNewId));
    }

    /// <summary>A nullable stored column.</summary>
    private static PhysicalColumn Column(string name, string columnType)
    {
        return new PhysicalColumn { Name = name, ColumnType = columnType, IsNullable = true, IsGenerated = false };
    }
}
