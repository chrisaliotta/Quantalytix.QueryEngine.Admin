namespace Quantalytix.QueryEngine.Admin.Application.Changes;

/// <summary>One statement of a commit's <see cref="WritePlan"/>.</summary>
/// <param name="Kind">What the write does.</param>
/// <param name="Depth">How far below the aggregate root its rows are: 0 for the root row, 1 for its children, and so on.</param>
/// <param name="Record">The record type of the written table.</param>
/// <param name="ExecuteAsync">Makes the write.</param>
public sealed record PlannedWrite(WriteKind Kind, int Depth, Type Record, Func<CancellationToken, Task> ExecuteAsync);
