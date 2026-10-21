namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>A level or member key that a live object names in a dimension, with what the object needs of it and the object as messages name it.</summary>
public sealed record KeyReference
{
    /// <summary>Whether the key names a level; otherwise it names a member.</summary>
    public required bool IsLevel { get; init; }

    /// <summary>The level or member key.</summary>
    public required string Key { get; init; }

    /// <summary>The object that names the key, such as <c>Binding 'Branch' of dataset 'loans'</c>.</summary>
    public required string Consumer { get; init; }

    /// <summary>What the object needs of the key; a level is only ever needed to exist.</summary>
    public KeyRequirement Requirement { get; init; } = KeyRequirement.Exists;

    /// <summary>A reference to a level.</summary>
    /// <param name="key">The level key.</param>
    /// <param name="consumer">The object that names it.</param>
    /// <returns>The reference.</returns>
    public static KeyReference Level(string key, string consumer)
    {
        return new KeyReference { IsLevel = true, Key = key, Consumer = consumer };
    }

    /// <summary>A reference to a member.</summary>
    /// <param name="key">The member key.</param>
    /// <param name="consumer">The object that names it.</param>
    /// <param name="requirement">What the object needs of the member.</param>
    /// <returns>The reference.</returns>
    public static KeyReference Member(string key, string consumer, KeyRequirement requirement = KeyRequirement.Exists)
    {
        return new KeyReference { IsLevel = false, Key = key, Consumer = consumer, Requirement = requirement };
    }
}
