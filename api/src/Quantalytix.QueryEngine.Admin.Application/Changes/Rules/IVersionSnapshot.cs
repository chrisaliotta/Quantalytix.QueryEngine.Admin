namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>A snapshot of one version of a versioned kind, which names the stored version its content came from.</summary>
public interface IVersionSnapshot
{
    /// <summary>The <c>version_kind</c> value, such as <c>dimension_version</c>.</summary>
    string Kind { get; }

    /// <summary>The stored version's surrogate key.</summary>
    long VersionId { get; }
}
