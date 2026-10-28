using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>The contract's <c>PinKind</c>: the identity kinds an assumption set pins, named by identity rather than version kind.</summary>
public static class PinKinds
{
    /// <summary>A dimension.</summary>
    public const string Dimension = "dimension";

    /// <summary>A mapping set.</summary>
    public const string MappingSet = "mapping_set";

    /// <summary>A bridge.</summary>
    public const string Bridge = "bridge";

    /// <summary>The pin kind of a living version kind.</summary>
    /// <param name="versionKind">A living <c>version_kind</c> value.</param>
    /// <returns>The pin kind.</returns>
    public static string Of(string versionKind)
    {
        return versionKind switch
        {
            VersionKinds.DimensionVersion => Dimension,
            VersionKinds.MappingVersion => MappingSet,
            _ => Bridge
        };
    }

    /// <summary>The order pins are listed in: dimensions, then mapping sets, then bridges.</summary>
    /// <param name="kind">A pin kind.</param>
    /// <returns>Its position.</returns>
    public static int Order(string kind)
    {
        return kind switch
        {
            Dimension => 0,
            MappingSet => 1,
            _ => 2
        };
    }
}
