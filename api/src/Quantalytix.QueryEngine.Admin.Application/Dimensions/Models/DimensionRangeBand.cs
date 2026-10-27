using System.Text.Json;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The contract's <c>RangeBand</c>, read and written: a bound is a JSON number for <c>decimal</c>, an ISO date for <c>date</c>, an
/// ISO date-time for <c>datetime</c>, or null for open. In a <c>saveBands</c> operation the member may be a temporary id.
/// </summary>
public sealed record DimensionRangeBand
{
    /// <summary>The position, starting at 1; <c>saveBands</c> renumbers the set in its order.</summary>
    public required int Ordinal { get; init; }

    /// <summary>The member the band assigns.</summary>
    public required string MemberId { get; init; }

    /// <summary>The lower bound, or null for none.</summary>
    public required JsonElement? Lower { get; init; }

    /// <summary>The upper bound, or null for none.</summary>
    public required JsonElement? Upper { get; init; }

    /// <summary>Whether a value equal to the lower bound falls in the band.</summary>
    public required bool LowerInclusive { get; init; }

    /// <summary>Whether a value equal to the upper bound falls in the band.</summary>
    public required bool UpperInclusive { get; init; }
}
