using System.Globalization;
using System.Text.Json;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Converts between the contract's JSON band bounds and the typed bound columns of <c>qe_range_band</c>: a JSON number for
/// <c>decimal</c>, an ISO date (<c>yyyy-MM-dd</c>) for <c>date</c>, an ISO date-time with a <c>Z</c> suffix for <c>datetime</c>, since
/// the tables store times as UTC (as <see cref="UtcRuleExtensions"/> requires of every time input).
/// </summary>
public static class DimensionBandBounds
{
    /// <summary>The format of a <c>date</c> bound.</summary>
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>The format a <c>datetime</c> bound is written in.</summary>
    private const string DatetimeFormat = "yyyy-MM-ddTHH:mm:ssZ";

    /// <summary>The bounds of a band row in its version's family.</summary>
    /// <param name="band">The row.</param>
    /// <param name="rangeDataType">The version's <c>range_data_type</c>; null when unset, which leaves every bound open.</param>
    /// <returns>The lower and upper bound as JSON, null where open.</returns>
    public static (JsonElement? Lower, JsonElement? Upper) Of(RangeBandRecord band, string? rangeDataType)
    {
        return rangeDataType switch
        {
            RangeDataTypes.Decimal => (Json(band.LowerDecimal), Json(band.UpperDecimal)),
            RangeDataTypes.Date => (Json(band.LowerDate, DateFormat), Json(band.UpperDate, DateFormat)),
            RangeDataTypes.Datetime => (Json(band.LowerDatetime, DatetimeFormat), Json(band.UpperDatetime, DatetimeFormat)),
            _ => (null, null)
        };
    }

    /// <summary>A band row with its bounds set from JSON in the version's family and every other family's left null.</summary>
    /// <param name="row">The row with every bound null.</param>
    /// <param name="lower">The lower bound, already checked by <see cref="Problem"/>.</param>
    /// <param name="upper">The upper bound, already checked by <see cref="Problem"/>.</param>
    /// <param name="rangeDataType">The version's <c>range_data_type</c>.</param>
    /// <returns>The row with its bounds.</returns>
    public static RangeBandRecord With(RangeBandRecord row, JsonElement? lower, JsonElement? upper, string rangeDataType)
    {
        return rangeDataType switch
        {
            RangeDataTypes.Decimal => row with { LowerDecimal = lower?.GetDecimal(), UpperDecimal = upper?.GetDecimal() },
            RangeDataTypes.Date => row with { LowerDate = Date(lower), UpperDate = Date(upper) },
            _ => row with { LowerDatetime = Datetime(lower), UpperDatetime = Datetime(upper) }
        };
    }

    /// <summary>A band row with every bound cleared, as a change of range data type leaves it.</summary>
    /// <param name="band">The row.</param>
    /// <returns>The row without bounds.</returns>
    public static RangeBandRecord Cleared(RangeBandRecord band)
    {
        return band with
        {
            LowerDecimal = null,
            UpperDecimal = null,
            LowerDate = null,
            UpperDate = null,
            LowerDatetime = null,
            UpperDatetime = null
        };
    }

    /// <summary>What is wrong with a JSON bound for a family, or null when it fits.</summary>
    /// <param name="bound">The bound; null is open and always fits.</param>
    /// <param name="rangeDataType">The version's <c>range_data_type</c>.</param>
    /// <returns>The message, or null.</returns>
    public static string? Problem(JsonElement? bound, string rangeDataType)
    {
        if (bound is not { } value || value.ValueKind == JsonValueKind.Null) return null;
        return rangeDataType switch
        {
            RangeDataTypes.Decimal => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _) ? null : "A decimal bound must be a number.",
            RangeDataTypes.Date => value.ValueKind == JsonValueKind.String && TryDate(value.GetString()!, out _) ? null : $"A date bound must be a date '{DateFormat}'.",
            _ => value.ValueKind == JsonValueKind.String && TryDatetime(value.GetString()!, out _) ? null : "A datetime bound must be a UTC date-time, such as 2026-09-30T00:00:00Z."
        };
    }

    /// <summary>A decimal bound as JSON.</summary>
    private static JsonElement? Json(decimal? value)
    {
        return value is { } number ? JsonSerializer.SerializeToElement(number) : null;
    }

    /// <summary>A date or date-time bound as a JSON string.</summary>
    private static JsonElement? Json(DateTime? value, string format)
    {
        return value is { } time ? JsonSerializer.SerializeToElement(time.ToString(format, CultureInfo.InvariantCulture)) : null;
    }

    /// <summary>A checked date bound.</summary>
    private static DateTime? Date(JsonElement? bound)
    {
        if (bound is not { ValueKind: JsonValueKind.String } value) return null;
        TryDate(value.GetString()!, out var date);
        return date;
    }

    /// <summary>A checked date-time bound, in UTC.</summary>
    private static DateTime? Datetime(JsonElement? bound)
    {
        if (bound is not { ValueKind: JsonValueKind.String } value) return null;
        TryDatetime(value.GetString()!, out var time);
        return time;
    }

    /// <summary>Parses an ISO date.</summary>
    private static bool TryDate(string text, out DateTime date)
    {
        return DateTime.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    /// <summary>Parses an ISO date-time in UTC.</summary>
    private static bool TryDatetime(string text, out DateTime time)
    {
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out time) && time.Kind == DateTimeKind.Utc;
    }
}
