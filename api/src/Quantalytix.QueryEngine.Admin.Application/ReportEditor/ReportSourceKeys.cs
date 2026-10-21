namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>The key a bridge source gives its bridge: the report's key and the source's key, unique because both are.</summary>
public static class ReportSourceKeys
{
    /// <summary>The key of a bridge source's bridge.</summary>
    /// <param name="reportKey">The report's key.</param>
    /// <param name="sourceKey">The source's key.</param>
    /// <returns>The bridge key.</returns>
    public static string Bridge(string reportKey, string sourceKey)
    {
        return $"{reportKey}_{sourceKey}";
    }
}
