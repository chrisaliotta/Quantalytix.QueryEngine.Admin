namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>
/// A check a published version must pass before it is retired, so retiring never takes away metadata that runtime queries
/// still read. Feature slices register their rules as <see cref="IRetirementRule"/>; the workflow runs every rule of the
/// kind on published to retired and rejects the transition when any rule reports a violation.
/// </summary>
public interface IRetirementRule : IVersionRule
{
}
