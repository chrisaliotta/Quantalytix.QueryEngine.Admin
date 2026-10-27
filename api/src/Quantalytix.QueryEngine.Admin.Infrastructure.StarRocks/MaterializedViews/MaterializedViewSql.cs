using Quantalytix.QueryEngine.Admin.Application.SourceObjects;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SchemaChanges;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.MaterializedViews;

/// <summary>
/// The DDL of the asynchronous materialized views of JSON path fields (OI-32), after StarRocks' documented syntax. A view reads every
/// column of its source object, so the planner can read the view in the object's place, plus one column per extracted value, typed
/// as the field casts it; <c>REFRESH ASYNC</c> refreshes it when the object's data changes.
/// </summary>
internal static class MaterializedViewSql
{
    /// <summary>Drops a view when it exists.</summary>
    /// <param name="viewName">The view.</param>
    /// <returns>The statement.</returns>
    public static string Drop(string viewName)
    {
        return $"DROP MATERIALIZED VIEW IF EXISTS {StarRocksDdl.Quote(viewName)}";
    }

    /// <summary>Creates a view.</summary>
    /// <param name="definition">The view, its source object and its extracted columns.</param>
    /// <returns>The statement.</returns>
    public static string Create(MaterializedViewDefinition definition)
    {
        var source = StarRocksDdl.Quote(definition.SourceObject);
        IEnumerable<string> columns = [$"{source}.*", .. definition.Columns.Select(column => $"{Extract(source, column)} AS {StarRocksDdl.Quote(column.Name)}")];
        return $"CREATE MATERIALIZED VIEW {StarRocksDdl.Quote(definition.ViewName)} REFRESH ASYNC AS SELECT {string.Join(", ", columns)} FROM {source}";
    }

    /// <summary>Starts a refresh of a view.</summary>
    /// <param name="viewName">The view.</param>
    /// <returns>The statement.</returns>
    public static string Refresh(string viewName)
    {
        return $"REFRESH MATERIALIZED VIEW {StarRocksDdl.Quote(viewName)}";
    }

    /// <summary>The expression extracting a column's value: JSON stays JSON, text stays text, every other type is cast.</summary>
    private static string Extract(string source, MaterializedViewColumn column)
    {
        var json = $"{source}.{StarRocksDdl.Quote(column.SourceColumn)}";
        return column.DataType switch
        {
            DataTypes.Json => $"json_query({json}, '{column.JsonPath}')",
            DataTypes.String => $"get_json_string({json}, '{column.JsonPath}')",
            _ => $"CAST(get_json_string({json}, '{column.JsonPath}') AS {StarRocksDdl.ColumnType(column.DataType, column.NumericPrecision, column.NumericScale)})"
        };
    }
}
