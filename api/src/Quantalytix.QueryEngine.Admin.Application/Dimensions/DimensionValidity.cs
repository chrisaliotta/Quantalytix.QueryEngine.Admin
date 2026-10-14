using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The half-open validity windows <c>[valid_from, valid_to)</c> of dimension versions, where a null <c>valid_to</c> is
/// open-ended. The query engine resolves the one published version whose window contains the request time.
/// </summary>
public static class DimensionValidity
{
    /// <summary>Whether two windows share an instant.</summary>
    /// <param name="first">A version with a validity start.</param>
    /// <param name="second">Another version with a validity start.</param>
    /// <returns>True when each window starts before the other one ends.</returns>
    public static bool Overlap(DimensionVersionRecord first, DimensionVersionRecord second)
    {
        return first.ValidFrom < (second.ValidTo ?? DateTime.MaxValue) && second.ValidFrom < (first.ValidTo ?? DateTime.MaxValue);
    }

    /// <summary>
    /// Whether a published version is the open-ended predecessor that publishing a later version closes: it has no end and
    /// starts before the later version.
    /// </summary>
    /// <param name="published">A published version of the dimension.</param>
    /// <param name="later">The version being validated or published.</param>
    /// <returns>True when publishing <paramref name="later"/> sets the predecessor's end to its start.</returns>
    public static bool IsOpenPredecessor(DimensionVersionRecord published, DimensionVersionRecord later)
    {
        return published.ValidTo is null && published.ValidFrom < later.ValidFrom;
    }

    /// <summary>A version and its window as messages show them, such as <c>v2 [2026-01-01 .. open)</c>.</summary>
    /// <param name="version">A dimension version.</param>
    /// <returns>The version number and window, with <c>open</c> for a missing bound.</returns>
    public static string Describe(DimensionVersionRecord version)
    {
        return $"v{version.VersionNumber} [{Bound(version.ValidFrom)} .. {Bound(version.ValidTo)})";
    }

    /// <summary>A bound as a date, or <c>open</c>.</summary>
    private static string Bound(DateTime? bound)
    {
        if (bound is null) return "open";
        return bound.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
