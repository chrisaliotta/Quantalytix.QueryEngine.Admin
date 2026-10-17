using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>The variables of calculation model versions: the logical inputs a profile fills from one of their sources.</summary>
public sealed class VariableService(
    ITableGateway<VariableRecord> table,
    ModelContentRemover remover,
    ReportUses reports,
    IIdGenerator ids)
    : ModelVersionRows<VariableRecord, VariableInput>(table, ids)
{
    /// <summary>Deletes a variable with its sources and their picks and override routes, unless a report column returns it.</summary>
    /// <param name="itemId">The variable.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>Null when the variable was deleted; otherwise the detail naming the report columns.</returns>
    public override async Task<string?> DeleteAsync(long itemId, CancellationToken cancellationToken)
    {
        var variable = await Rows.GetAsync(new { VariableId = itemId }, cancellationToken);
        var blockers = new DeleteBlockers($"Variable '{variable.VariableKey}'");
        blockers.AddNamed("returned by report columns", await reports.VariableColumnsAsync([itemId], cancellationToken));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;
        await remover.RemoveVariablesAsync([itemId], cancellationToken);
        return null;
    }

    /// <summary>The gateway key of a variable.</summary>
    /// <param name="itemId">The variable ID.</param>
    /// <returns>The key object.</returns>
    protected override object Key(long itemId)
    {
        return new { VariableId = itemId };
    }

    /// <summary>The model version a variable belongs to.</summary>
    /// <param name="row">The variable.</param>
    /// <returns>Its model version ID.</returns>
    protected override long VersionOf(VariableRecord row)
    {
        return row.ModelVersionId;
    }

    /// <summary>The key a variable is listed by.</summary>
    /// <param name="row">The variable.</param>
    /// <returns>Its key.</returns>
    protected override string KeyOf(VariableRecord row)
    {
        return row.VariableKey;
    }

    /// <summary>The variable row an input describes.</summary>
    /// <param name="itemId">The variable ID.</param>
    /// <param name="versionId">The model version.</param>
    /// <param name="input">The validated input.</param>
    /// <returns>The row to store.</returns>
    protected override VariableRecord ToRecord(long itemId, long versionId, VariableInput input)
    {
        return new VariableRecord
        {
            VariableId = itemId,
            ModelVersionId = versionId,
            VariableKey = input.VariableKey,
            DisplayName = input.DisplayName,
            VariableRole = input.VariableRole,
            TargetMemberKey = input.TargetMemberKey,
            SelectionScope = input.SelectionScope,
            MissingBehavior = input.MissingBehavior
        };
    }
}
