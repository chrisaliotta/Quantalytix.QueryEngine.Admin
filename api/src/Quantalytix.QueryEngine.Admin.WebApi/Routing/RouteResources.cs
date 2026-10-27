using System.Collections.Frozen;

namespace Quantalytix.QueryEngine.Admin.WebApi.Routing;

/// <summary>Finds the registered resources a route template names, each with the route value that identifies it.</summary>
/// <param name="resources">Every registered resource.</param>
public sealed class RouteResources(IEnumerable<RouteResource> resources)
{
    /// <summary>The resources by segment.</summary>
    private readonly FrozenDictionary<string, RouteResource> _bySegment = resources.ToFrozenDictionary(resource => resource.Segment, StringComparer.Ordinal);

    /// <summary>The resources a template names, outermost first: every registered literal segment followed by a parameter.</summary>
    /// <param name="template">A route template, such as <c>api/tenants/{tenantId}/dimension-versions/{versionId:long}/members</c>.</param>
    /// <returns>Each resource with the name of the route value after its segment.</returns>
    public IReadOnlyList<(RouteResource Resource, string Parameter)> In(string template)
    {
        var segments = template.Split('/');
        var found = new List<(RouteResource, string)>();
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (_bySegment.TryGetValue(segments[index], out var resource) && ParameterName(segments[index + 1]) is { } parameter)
                found.Add((resource, parameter));
        }

        return found;
    }

    /// <summary>The name of a <c>{name}</c> or <c>{name:constraint}</c> segment, or null for a literal.</summary>
    private static string? ParameterName(string segment)
    {
        if (!segment.StartsWith('{') || !segment.EndsWith('}')) return null;
        return segment[1..^1].Split(':')[0].TrimEnd('?');
    }
}
