namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// A semantic check a version must pass before it leaves draft (draft to validated; draft to published for assumption
/// sets). Feature slices register their rules as <see cref="IPublicationRule"/>; the workflow runs every rule of the kind
/// and rejects the transition when any rule reports a violation.
/// </summary>
public interface IPublicationRule : IVersionRule
{
}
