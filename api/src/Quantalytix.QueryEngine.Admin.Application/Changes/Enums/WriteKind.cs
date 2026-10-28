namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>What a planned write does to its rows.</summary>
public enum WriteKind
{
    /// <summary>Removes rows.</summary>
    Delete,

    /// <summary>Replaces one row's non-key columns.</summary>
    Update,

    /// <summary>Adds rows (an upsert in a StarRocks primary-key table).</summary>
    Insert,

    /// <summary>Changes the physical schema (DDL), such as adding a managed table's columns.</summary>
    Schema
}
