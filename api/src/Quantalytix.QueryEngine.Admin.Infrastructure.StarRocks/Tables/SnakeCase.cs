using System.Text;

namespace Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Tables;

/// <summary>Converts record property names to the column names they mirror.</summary>
internal static class SnakeCase
{
    /// <summary>
    /// Inserts an underscore before every uppercase letter except the first, then lowercases, so <c>LowerDatetime</c>
    /// becomes <c>lower_datetime</c>. Dapper maps the column back by matching names without underscores.
    /// </summary>
    /// <param name="propertyName">A PascalCase property name.</param>
    /// <returns>The column name.</returns>
    public static string From(string propertyName)
    {
        var column = new StringBuilder(propertyName.Length + 8);
        for (var index = 0; index < propertyName.Length; index++)
        {
            if (index > 0 && char.IsUpper(propertyName[index])) column.Append('_');
            column.Append(char.ToLowerInvariant(propertyName[index]));
        }

        return column.ToString();
    }
}
