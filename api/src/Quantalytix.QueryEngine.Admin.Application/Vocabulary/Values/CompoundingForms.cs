namespace Quantalytix.QueryEngine.Admin.Application.Vocabulary;

/// <summary>The <c>compounding_form</c> vocabulary: how a compound measure links its per-step rates.</summary>
public static class CompoundingForms
{
    /// <summary><c>1 - prod(1 - r)</c>, and 1 when any step's rate is at least 1.</summary>
    public const string Decay = "decay";

    /// <summary><c>prod(1 + r) - 1</c>, and -1 when any step's rate is at most -1.</summary>
    public const string Growth = "growth";

    /// <summary>The vocabulary's definition, as the web client and validators read it.</summary>
    public static readonly Vocabulary Definition = new()
    {
        Name = "compounding_form",
        Key = "compoundingForm",
        Description = "How a compound measure links its per-step rates.",
        Values =
        [
            new() { Value = Decay, Label = "Decay", Description = "1 - prod(1 - r), such as a survival-based mortality rate." },
            new() { Value = Growth, Label = "Growth", Description = "prod(1 + r) - 1, such as a compounded return." }
        ]
    };
}
