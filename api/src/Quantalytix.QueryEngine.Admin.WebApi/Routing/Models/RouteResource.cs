namespace Quantalytix.QueryEngine.Admin.WebApi.Routing;

/// <summary>
/// A resource a route segment names: in <c>dimension-versions/{versionId}</c>, the segment <c>dimension-versions</c> names a
/// <c>qe_dimension_version</c> row by the route value that follows it. Registered by each module through
/// <see cref="RouteResourceServiceCollectionExtensions"/>.
/// </summary>
/// <param name="Segment">The literal route segment, such as <c>dimension-versions</c>.</param>
/// <param name="ExistsAsync">Whether the row the value after the segment names exists: request services, the value, every route value.</param>
public sealed record RouteResource(
    string Segment,
    Func<IServiceProvider, string, RouteValueDictionary, CancellationToken, Task<bool>> ExistsAsync);
