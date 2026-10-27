using System.Diagnostics.CodeAnalysis;

namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>The result of a change set or lifecycle command: its value, or the <see cref="Refusal"/> that kept it from writing.</summary>
/// <typeparam name="T">The value of a success.</typeparam>
public sealed record Outcome<T> where T : class
{
    /// <summary>The value; null when refused.</summary>
    public T? Value { get; private init; }

    /// <summary>Why nothing was written; null on success.</summary>
    public Refusal? Refusal { get; private init; }

    /// <summary>Whether the command succeeded, in which case <see cref="Value"/> is set.</summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Refusal))]
    public bool Succeeded => Refusal is null;

    /// <summary>A success.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The outcome.</returns>
    public static Outcome<T> Success(T value)
    {
        return new Outcome<T> { Value = value };
    }

    /// <summary>A refusal.</summary>
    /// <param name="refusal">Why nothing was written.</param>
    /// <returns>The outcome.</returns>
    public static Outcome<T> Refused(Refusal refusal)
    {
        return new Outcome<T> { Refusal = refusal };
    }
}
