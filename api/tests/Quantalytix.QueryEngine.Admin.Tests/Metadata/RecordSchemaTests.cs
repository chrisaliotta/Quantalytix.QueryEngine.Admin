using System.Collections.Frozen;
using System.Reflection;
using System.Runtime.CompilerServices;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Tables;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Metadata;

/// <summary>
/// Confirms that every metadata record mirrors its table 1:1: its snake_case properties are the DDL columns in order, with
/// the column's type and nullability, and its catalog entry names the table's PRIMARY KEY and JSON columns. The table
/// gateway generates every statement from the record and its catalog entry, so a record that drifts from its table breaks
/// the generated SQL or the mapping of rows onto records.
/// </summary>
public sealed class RecordSchemaTests
{
    /// <summary>
    /// The control-plane table, which the tenant DDL script does not contain, as the admin API specification defines it:
    /// <c>query_control.qe_tenant_directory</c>.
    /// </summary>
    private const string TenantDirectoryDdl = """
        CREATE TABLE qe_tenant_directory
        (
            tenant_id VARCHAR(128) NOT NULL,
            provider_id VARCHAR(128) NOT NULL,
            connection_string VARCHAR(4096) NOT NULL,
            is_enabled BOOLEAN NOT NULL DEFAULT '1',
            metadata_cache_seconds INT NOT NULL DEFAULT '60',
            updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
        )
        PRIMARY KEY (tenant_id)
        """;

    /// <summary>The CLR type of each SQL type: DATE and DATETIME read as DateTime, JSON columns as raw JSON text.</summary>
    private static readonly FrozenDictionary<string, Type> ClrTypes = new Dictionary<string, Type>
    {
        ["BIGINT"] = typeof(long),
        ["INT"] = typeof(int),
        ["TINYINT"] = typeof(int),
        ["BOOLEAN"] = typeof(bool),
        ["DECIMAL"] = typeof(decimal),
        ["VARCHAR"] = typeof(string),
        ["DATE"] = typeof(DateTime),
        ["DATETIME"] = typeof(DateTime),
        ["JSON"] = typeof(string)
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The tenant tables of the schema's DDL script, which the test project copies to its output, plus the control-plane table.</summary>
    private static readonly FrozenDictionary<string, DdlTable> Tables =
        SchemaDdl.Parse($"{File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Schema", "ddl_srdb_bi.sql"))}\n{TenantDirectoryDdl}");

    /// <summary>Every record type in the table catalog.</summary>
    public static TheoryData<Type> Records => [.. TableCatalog.Tables.Keys];

    /// <summary>The record's properties, in snake_case and declaration order, are the table's columns in DDL order.</summary>
    /// <param name="record">A metadata record type.</param>
    [Theory]
    [MemberData(nameof(Records))]
    public void Columns_AreTheTableColumnsInOrder(Type record)
    {
        var columns = TableOf(record).Columns.Select(column => column.Name);
        Assert.Equal(columns, DeclaredProperties.Of(record).Select(property => SnakeCase.From(property.Name)));
    }

    /// <summary>Each property has its column's CLR type; NOT NULL columns are required and non-nullable, NULL columns nullable.</summary>
    /// <param name="record">A metadata record type.</param>
    [Theory]
    [MemberData(nameof(Records))]
    public void PropertyTypes_FollowTheColumnTypes(Type record)
    {
        var expected = TableOf(record).Columns.Select(column => Describe(column.Name, ClrTypes[column.Type], column.IsNullable, !column.IsNullable));
        Assert.Equal(expected, DeclaredProperties.Of(record).Select(Describe));
    }

    /// <summary>The catalog's key properties, in snake_case, are the table's PRIMARY KEY in key order.</summary>
    /// <param name="record">A metadata record type.</param>
    [Theory]
    [MemberData(nameof(Records))]
    public void KeyProperties_AreThePrimaryKey(Type record)
    {
        Assert.Equal(TableOf(record).PrimaryKey, TableCatalog.Tables[record].KeyProperties.Select(SnakeCase.From));
    }

    /// <summary>The catalog's JSON properties are exactly the table's JSON columns, so every JSON value is written through PARSE_JSON.</summary>
    /// <param name="record">A metadata record type.</param>
    [Theory]
    [MemberData(nameof(Records))]
    public void JsonProperties_AreTheJsonColumns(Type record)
    {
        var jsonColumns = TableOf(record).Columns.Where(column => column.Type == "JSON").Select(column => column.Name);
        Assert.Equal(jsonColumns, TableCatalog.Tables[record].JsonProperties.Select(SnakeCase.From));
    }

    /// <summary>The DDL of the table a record mirrors.</summary>
    private static DdlTable TableOf(Type record)
    {
        return Tables[TableCatalog.Tables[record].Name];
    }

    /// <summary>Describes a record property the way <see cref="Describe(string, Type, bool, bool)"/> describes a column.</summary>
    private static string Describe(PropertyInfo property)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var isNullable = new NullabilityInfoContext().Create(property).ReadState == NullabilityState.Nullable;
        return Describe(SnakeCase.From(property.Name), type, isNullable, property.IsDefined(typeof(RequiredMemberAttribute)));
    }

    /// <summary>A comparable description such as <c>dataset_id: Int64 required</c> or <c>published_at: DateTime?</c>.</summary>
    private static string Describe(string column, Type type, bool isNullable, bool isRequired)
    {
        var description = $"{column}: {type.Name}";
        if (isNullable) description += "?";
        if (isRequired) description += " required";
        return description;
    }
}
