using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// The content hash a release gets on publish (R13, OI-41): SHA-256 over the manifest's <c>dataset_id:dataset_version_id</c> pairs,
/// sorted and one per line, as lowercase hex. Two releases that pin the same versions have the same hash.
/// </summary>
public static class ReleaseContentHash
{
    /// <summary>The hash of a manifest.</summary>
    /// <param name="manifest">The manifest rows, in any order.</param>
    /// <returns>64 lowercase hex digits.</returns>
    public static string Of(IEnumerable<ReleaseDatasetRecord> manifest)
    {
        var text = string.Join('\n', manifest
            .OrderBy(row => row.DatasetId)
            .ThenBy(row => row.DatasetVersionId)
            .Select(row => string.Create(CultureInfo.InvariantCulture, $"{row.DatasetId}:{row.DatasetVersionId}")));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
