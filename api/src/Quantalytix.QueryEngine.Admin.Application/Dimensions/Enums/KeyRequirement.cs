namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>What a live object needs of the level or member key it names in the version it resolves the key in.</summary>
public enum KeyRequirement
{
    /// <summary>The version has the level or member.</summary>
    Exists,

    /// <summary>The version has the member, and the member is assignable, as a scenario version a comparison reads is.</summary>
    Assignable,

    /// <summary>The version has an assignable member somewhere under the member, as a folder a comparison reads the latest version of does.</summary>
    AssignableBelow
}
