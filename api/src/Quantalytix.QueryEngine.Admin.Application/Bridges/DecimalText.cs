using System.Globalization;

namespace Quantalytix.QueryEngine.Admin.Application.Bridges;

/// <summary>Writes factors and weights into rule messages without the trailing zeros of their <c>DECIMAL(38, 12)</c> columns.</summary>
internal static class DecimalText
{
    /// <summary>A decimal as messages show it, such as <c>0.9</c> for a stored <c>0.900000000000</c>.</summary>
    /// <param name="value">A factor, weight or weight sum.</param>
    /// <returns>The value with its significant decimals only.</returns>
    public static string Of(decimal value)
    {
        return value.ToString("0.############", CultureInfo.InvariantCulture);
    }
}
