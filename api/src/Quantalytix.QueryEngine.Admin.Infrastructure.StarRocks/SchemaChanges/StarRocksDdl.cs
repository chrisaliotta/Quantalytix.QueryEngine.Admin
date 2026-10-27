using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.SchemaChanges;

/// <summary>
/// The text pieces of the StarRocks DDL the API runs. DDL binds no parameters, so identifiers are quoted here and every inlined
/// name or JSON path has already matched a pattern that admits no quotes.
/// </summary>
internal static class StarRocksDdl
{
    /// <summary>Quotes an identifier with backticks, doubling any backtick inside it.</summary>
    /// <param name="identifier">A table, view or column name.</param>
    /// <returns>The quoted identifier.</returns>
    public static string Quote(string identifier)
    {
        return $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";
    }

    /// <summary>The column type of a field type; <c>timestamp</c> values are stored as <c>DATETIME</c>.</summary>
    /// <param name="dataType">A <c>data_type</c> value.</param>
    /// <param name="precision">The decimal precision; null unless decimal.</param>
    /// <param name="scale">The decimal scale; null unless decimal.</param>
    /// <returns>The type, such as <c>DECIMAL(18, 2)</c>.</returns>
    public static string ColumnType(string dataType, int? precision, int? scale)
    {
        return dataType switch
        {
            DataTypes.String => "VARCHAR(65533)",
            DataTypes.Boolean => "BOOLEAN",
            DataTypes.Int32 => "INT",
            DataTypes.Int64 => "BIGINT",
            DataTypes.Decimal => string.Create(CultureInfo.InvariantCulture, $"DECIMAL({precision}, {scale})"),
            DataTypes.Date => "DATE",
            DataTypes.Json => "JSON",
            _ => "DATETIME"
        };
    }
}
