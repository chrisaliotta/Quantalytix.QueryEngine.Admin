using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The identity, version and role operations of the dimension change set. Only the display name and flags change (M1); a system
/// dimension is never removable. Role keys are tenant-unique (M6); time dimensions have no roles; a profile may allow one role,
/// and a dimension whose role a report names as a scenario takes no second one (M7); every other dimension keeps at least one.
/// </summary>
internal static class DimensionIdentityEdits
{
    /// <summary><c>updateDimension</c>.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Takes refusals.</param>
    /// <returns>The aggregate with the patched identity.</returns>
    public static DimensionSnapshot Update(DimensionSnapshot snapshot, DimensionUpdateDimensionOperation operation, ChangeContext context)
    {
        var patch = operation.Patch;
        if (patch.IsRemovable is not null && snapshot.Dimension.DefinitionOrigin == DefinitionOrigins.System)
        {
            context.Conflict("System dimensions are never removable", $"'{snapshot.Dimension.DimensionKey}' is a system dimension.");
            return snapshot;
        }

        return snapshot with
        {
            Dimension = snapshot.Dimension with
            {
                DisplayName = patch.DisplayName ?? snapshot.Dimension.DisplayName,
                IsEnabled = patch.IsEnabled ?? snapshot.Dimension.IsEnabled,
                IsRemovable = patch.IsRemovable ?? snapshot.Dimension.IsRemovable
            }
        };
    }

    /// <summary><c>updateVersion</c>: a changed range data type clears every band bound, which the old type typed.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Takes refusals.</param>
    /// <returns>The aggregate with the patched version.</returns>
    public static DimensionSnapshot UpdateVersion(DimensionSnapshot snapshot, DimensionUpdateVersionOperation operation, ChangeContext context)
    {
        if (snapshot.Dimension.DimensionKind != DimensionKinds.Range)
        {
            context.Conflict("Not a range dimension", "Only range dimensions have a range data type.");
            return snapshot;
        }

        var rangeDataType = operation.Patch.RangeDataType;
        if (rangeDataType == snapshot.Version.RangeDataType) return snapshot;
        return snapshot with
        {
            Version = snapshot.Version with { RangeDataType = rangeDataType },
            Bands = [.. snapshot.Bands.Select(DimensionBandBounds.Cleared)]
        };
    }

    /// <summary><c>saveRole</c>.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the role saved.</returns>
    public static DimensionSnapshot SaveRole(DimensionSnapshot snapshot, DimensionSaveRoleOperation operation, ChangeContext context)
    {
        if (snapshot.Dimension.DimensionKind == DimensionKinds.Time)
        {
            context.Conflict("Time dimensions have no roles", $"'{snapshot.Dimension.DimensionKey}' is a time dimension.");
            return snapshot;
        }

        var input = operation.Input;
        var roleId = DimensionEditTargets.SavedId(input.Id, context);
        var existing = snapshot.Roles.SingleOrDefault(role => role.DimensionRoleId == roleId);
        if (DimensionEditTargets.IsGone(input.Id, existing is not null, "Role", context)) return snapshot;
        if (existing is null && SecondRoleRefusal(snapshot) is { } refusal)
        {
            context.Conflict("Cannot add a role", refusal);
            return snapshot;
        }

        var key = existing?.DimensionRoleKey ?? input.Key;
        if (existing is null && (snapshot.Surroundings.OtherRoleKeys.Contains(key) || snapshot.Roles.Any(role => string.Equals(role.DimensionRoleKey, key, StringComparison.OrdinalIgnoreCase))))
        {
            context.Reject("input.key", $"The role key '{key}' is already used by a dimension.");
            return snapshot;
        }

        var saved = new DimensionRoleRecord
        {
            DimensionRoleId = roleId,
            DimensionId = snapshot.Dimension.DimensionId,
            DimensionRoleKey = key,
            DisplayName = input.DisplayName,
            Description = input.Description
        };
        return snapshot with { Roles = [.. snapshot.Roles.Where(role => role.DimensionRoleId != roleId), saved] };
    }

    /// <summary><c>deleteRole</c>.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate without the role.</returns>
    public static DimensionSnapshot DeleteRole(DimensionSnapshot snapshot, DimensionDeleteRoleOperation operation, ChangeContext context)
    {
        if (DimensionEditTargets.Role(snapshot, operation.RoleId, context) is not { } role) return snapshot;
        var check = DimensionDeleteRules.Role(snapshot, role);
        if (!check.Allowed)
        {
            context.Blocked(check);
            return snapshot;
        }

        if (snapshot.Roles.Count == 1)
        {
            context.Conflict("Cannot delete the last role", $"Dimension '{snapshot.Dimension.DimensionKey}' needs at least one role, which bindings bind.");
            return snapshot;
        }

        return snapshot with { Roles = [.. snapshot.Roles.Where(row => row.DimensionRoleId != role.DimensionRoleId)] };
    }

    /// <summary>Why the dimension takes no second role, or null when it may.</summary>
    private static string? SecondRoleRefusal(DimensionSnapshot snapshot)
    {
        if (snapshot.Profile?.SingleRole == true) return $"The {snapshot.Profile.DisplayName} profile allows exactly one role.";
        var scenarios = snapshot.Surroundings.Usage.Where(reference => reference.Kind == DimensionUsageKinds.ReportScenario).Select(reference => $"'{reference.Name}'").ToList();
        if (scenarios.Count == 0) return null;
        return $"Role '{snapshot.Roles[0].DimensionRoleKey}' is a scenario of reports {string.Join(", ", scenarios)}, and a scenario's dimension has exactly one role.";
    }
}
