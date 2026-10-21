using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>dimension_consumers_resolve</c>: every level and member key that live objects name in the dimension resolves in the version
/// as each object needs it, because publishing it makes it the version they resolve their keys in: the key exists, and a scenario
/// a default comparison reads is an assignable member, or a folder with an assignable member under it. <see cref="DimensionConsumers"/>
/// lists the objects; <see cref="DimensionChanges"/> holds live edits of the published version to the same rule.
/// </summary>
public sealed class DimensionConsumersResolveRule(
    ITableGateway<DimensionVersionRecord> versions,
    DimensionConsumers consumers,
    PublishedDimensionReader reader)
    : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "dimension_consumers_resolve";

    /// <summary>The kind the rule checks: dimension versions.</summary>
    public string Kind { get; } = VersionKinds.DimensionVersion;

    /// <summary>Reports every key a live object names that the version does not resolve as the object needs it.</summary>
    /// <param name="versionId">The dimension version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per unresolved key reference.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { DimensionVersionId = versionId }, cancellationToken);
        var keys = await reader.KeysAsync(version, cancellationToken);
        return [.. (await consumers.ListAsync(version.DimensionId, cancellationToken)).Where(reference => !keys.Resolves(reference)).Select(reference => Violation(reference, version))];
    }

    /// <summary>The violation of a reference a version does not resolve, as stored or as a write would leave it.</summary>
    /// <param name="reference">The unresolved reference.</param>
    /// <param name="version">The dimension version.</param>
    /// <returns>The violation, naming the object and what the version lacks.</returns>
    public static RuleViolation Violation(KeyReference reference, DimensionVersionRecord version)
    {
        var name = $"v{version.VersionNumber}";
        var message = reference switch
        {
            { IsLevel: true } => $"{reference.Consumer} names level '{reference.Key}', which {name} lacks.",
            { Requirement: KeyRequirement.Assignable } => $"{reference.Consumer} names member '{reference.Key}', which is not an assignable member of {name}.",
            { Requirement: KeyRequirement.AssignableBelow } => $"{reference.Consumer} names member '{reference.Key}', under which {name} has no assignable member.",
            _ => $"{reference.Consumer} names member '{reference.Key}', which {name} lacks."
        };
        return new RuleViolation { Rule = Code, Message = message };
    }
}
