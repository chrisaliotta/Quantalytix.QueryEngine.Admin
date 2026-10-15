namespace Quantalytix.QueryEngine.Admin.Application.Tenancy;

/// <summary>
/// The IDs the current request's route names, for an input rule that checks a body value against the route, such as a
/// clone source that must be a version of the route's dataset. Input validators see one action argument at a time, so they
/// read the route here.
/// </summary>
public interface IRequestRoute
{
    /// <summary>The ID a route placeholder holds.</summary>
    /// <param name="placeholder">The placeholder's name, such as <c>datasetId</c>.</param>
    /// <returns>The ID.</returns>
    long Id(string placeholder);
}
