using System.Globalization;
using System.Text.RegularExpressions;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.SourceObjects;

/// <summary>
/// Maps a StarRocks column type to the logical type of a field that reads the column. The columns endpoint, the field
/// pickers and bulk field creation all use this one mapping; a type with no logical equivalent maps to nothing and is not
/// offered.
/// </summary>
public static partial class ColumnTypeMapping
{
    /// <summary>The largest decimal precision StarRocks supports, which LARGEINT and floating-point columns map to.</summary>
    private const int MaxPrecision = 38;

    /// <summary>The scale floating-point columns map to.</summary>
    private const int FloatingPointScale = 12;

    /// <summary>How information_schema writes a BOOLEAN column; a TINYINT column reads <c>tinyint(4)</c>.</summary>
    private const string BooleanColumnType = "tinyint(1)";

    /// <summary>How information_schema writes a LARGEINT column; StarRocks has no unsigned BIGINT.</summary>
    private const string LargeIntColumnType = "bigint(20) unsigned";

    /// <summary>Maps a column type, such as <c>varchar(16)</c> or <c>decimal(20, 2)</c>, to a field type.</summary>
    /// <param name="columnType">
    /// The column type as information_schema writes it; case is ignored, and so is a length or display width except the
    /// ones that tell BOOLEAN and LARGEINT apart from TINYINT and BIGINT.
    /// </param>
    /// <returns>The field type, or null when the column type has no logical equivalent.</returns>
    public static FieldType? Map(string columnType)
    {
        var type = columnType.Trim().ToLowerInvariant();
        var decimalType = DecimalType().Match(type);
        if (decimalType.Success) return Decimal(Number(decimalType.Groups["precision"]), Number(decimalType.Groups["scale"]));
        if (type == BooleanColumnType) return Logical(DataTypes.Boolean);
        if (type == LargeIntColumnType) return Decimal(MaxPrecision, 0);
        return BaseName(type) switch
        {
            "varchar" or "char" => Logical(DataTypes.String),
            "tinyint" or "smallint" or "int" => Logical(DataTypes.Int32),
            "bigint" => Logical(DataTypes.Int64),
            "float" or "double" => Decimal(MaxPrecision, FloatingPointScale),
            "date" => Logical(DataTypes.Date),
            "datetime" => Logical(DataTypes.Datetime),
            "json" => Logical(DataTypes.Json),
            _ => null
        };
    }

    /// <summary>The type's name without its length, display width or modifiers: <c>int(11)</c> is <c>int</c>.</summary>
    private static string BaseName(string type)
    {
        return type.Split('(', ' ')[0];
    }

    /// <summary>A field type without precision or scale.</summary>
    private static FieldType Logical(string dataType)
    {
        return new FieldType { DataType = dataType };
    }

    /// <summary>A decimal field type.</summary>
    private static FieldType Decimal(int precision, int scale)
    {
        return new FieldType { DataType = DataTypes.Decimal, NumericPrecision = precision, NumericScale = scale };
    }

    /// <summary>The integer a regular expression group captured.</summary>
    private static int Number(Group group)
    {
        return int.Parse(group.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>Matches a decimal type with its precision and scale, such as <c>decimal(20,2)</c> or <c>decimal64(10, 2)</c>.</summary>
    [GeneratedRegex(@"^decimal\d*\((?<precision>\d+),\s*(?<scale>\d+)\)$")]
    private static partial Regex DecimalType();
}
