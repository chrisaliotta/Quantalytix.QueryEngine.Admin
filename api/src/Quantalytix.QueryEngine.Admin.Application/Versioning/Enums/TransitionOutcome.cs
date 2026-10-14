namespace Quantalytix.QueryEngine.Admin.Application.Versioning;

/// <summary>How a requested status transition ended.</summary>
public enum TransitionOutcome
{
    /// <summary>The status changed.</summary>
    Applied,

    /// <summary>The lifecycle does not allow moving from the current status to the target; nothing changed.</summary>
    NotAllowed,

    /// <summary>Publication rules found violations; the status is unchanged.</summary>
    Rejected
}
