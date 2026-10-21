namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// The keys a design source gives the objects it writes: its bridge takes the report's key and the source's key, and each of its
/// variable sources the source's key and the variable's key, so every key is a well-formed key unique where it must be.
/// </summary>
public static class DesignSourceKeys
{
    /// <summary>The key of a design source's bridge.</summary>
    /// <param name="reportKey">The report's key.</param>
    /// <param name="sourceKey">The design source's key.</param>
    /// <returns>The bridge key.</returns>
    public static string Bridge(string reportKey, string sourceKey)
    {
        return $"{reportKey}_{sourceKey}";
    }

    /// <summary>The key of the design source a bridge of the report holds: its key after the report's prefix, or the whole key of a bridge the design did not create.</summary>
    /// <param name="reportKey">The report's key.</param>
    /// <param name="bridgeKey">The key of a bridge into the report's dimension.</param>
    /// <returns>The design source's key.</returns>
    public static string Source(string reportKey, string bridgeKey)
    {
        var prefix = $"{reportKey}_";
        return bridgeKey.StartsWith(prefix, StringComparison.Ordinal) ? bridgeKey[prefix.Length..] : bridgeKey;
    }

    /// <summary>The key of the variable source a design source writes for one variable.</summary>
    /// <param name="sourceKey">The design source's key.</param>
    /// <param name="variableKey">The variable's key.</param>
    /// <returns>The variable source key.</returns>
    public static string VariableSource(string sourceKey, string variableKey)
    {
        return $"{sourceKey}_{variableKey}";
    }
}
