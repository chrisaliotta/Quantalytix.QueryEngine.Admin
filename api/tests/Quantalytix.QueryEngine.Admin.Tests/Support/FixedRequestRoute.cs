using Quantalytix.QueryEngine.Admin.Application.Tenancy;

namespace Quantalytix.QueryEngine.Admin.Tests.Support;

/// <summary>A request route with fixed IDs, for tests that run input validators outside an HTTP request.</summary>
/// <param name="ids">The ID of each route placeholder, such as <c>dimensionId</c>.</param>
public sealed class FixedRequestRoute(IReadOnlyDictionary<string, long> ids) : IRequestRoute
{
    /// <summary>The ID a route placeholder holds.</summary>
    /// <param name="placeholder">The placeholder's name.</param>
    /// <returns>The fixed ID.</returns>
    public long Id(string placeholder)
    {
        return ids[placeholder];
    }

    /// <summary>The ID a route placeholder holds, or null when the route has none.</summary>
    /// <param name="placeholder">The placeholder's name.</param>
    /// <returns>The fixed ID, or null.</returns>
    public long? OptionalId(string placeholder)
    {
        return ids.TryGetValue(placeholder, out var id) ? id : null;
    }
}
