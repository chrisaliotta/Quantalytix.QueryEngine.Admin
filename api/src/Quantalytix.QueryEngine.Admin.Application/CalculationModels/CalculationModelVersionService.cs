using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>Reads, creates, clones, edits and deletes the versions of calculation models. Status changes go through the version workflow.</summary>
public sealed class CalculationModelVersionService(
    ITableGateway<CalculationModelRecord> models,
    ITableGateway<CalculationModelVersionRecord> versions,
    ModelVersionCloner cloner,
    ModelContentRemover remover,
    ReportUses reports,
    IIdGenerator ids)
{
    /// <summary>The calendar every model version follows.</summary>
    private const string Gregorian = "gregorian";

    /// <summary>The grammar version of every model formula: the query engine's expression language.</summary>
    private const string FormulaLanguageVersion = "1.0";

    /// <summary>Lists a model's versions.</summary>
    /// <param name="modelId">The model.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The versions by version number.</returns>
    public async Task<IReadOnlyList<CalculationModelVersionRecord>> ListAsync(long modelId, CancellationToken cancellationToken)
    {
        var rows = await versions.ListAsync(new { ModelId = modelId }, cancellationToken);
        return [.. rows.OrderBy(row => row.VersionNumber)];
    }

    /// <summary>Reads one model version.</summary>
    /// <param name="versionId">The model version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The version.</returns>
    public Task<CalculationModelVersionRecord> GetAsync(long versionId, CancellationToken cancellationToken)
    {
        return versions.GetAsync(new { ModelVersionId = versionId }, cancellationToken);
    }

    /// <summary>Creates a draft with the model's next version number: empty, or with a copy of another version's content.</summary>
    /// <param name="modelId">The model.</param>
    /// <param name="input">The currency and the optional clone source.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The new draft.</returns>
    public async Task<CalculationModelVersionRecord> CreateAsync(long modelId, NewModelVersionInput input, CancellationToken cancellationToken)
    {
        var existing = await versions.ListAsync(new { ModelId = modelId }, cancellationToken);
        var version = new CalculationModelVersionRecord
        {
            ModelVersionId = ids.NextId(),
            ModelId = modelId,
            VersionNumber = VersionNumbers.Next(existing.Select(row => row.VersionNumber)),
            Status = PublicationStatuses.Draft,
            CalendarKey = Gregorian,
            CurrencyCode = input.CurrencyCode,
            FormulaLanguageVersion = FormulaLanguageVersion
        };
        await versions.InsertAsync(version, cancellationToken);
        if (input.CloneFromVersionId is { } sourceVersionId) await cloner.CloneAsync(sourceVersionId, version.ModelVersionId, cancellationToken);
        return version;
    }

    /// <summary>Replaces a draft version's currency.</summary>
    /// <param name="versionId">The draft model version.</param>
    /// <param name="input">The new attributes.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The stored version.</returns>
    public async Task<CalculationModelVersionRecord> UpdateAsync(long versionId, ModelVersionInput input, CancellationToken cancellationToken)
    {
        var updated = await GetAsync(versionId, cancellationToken) with { CurrencyCode = input.CurrencyCode };
        await versions.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    /// <summary>
    /// Deletes a draft version that no report reads, with its content, and clears the model's current-version pointer when it
    /// named the version.
    /// </summary>
    /// <param name="versionId">The draft model version.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>Null when the version was deleted; otherwise the detail naming the report versions, and nothing was deleted.</returns>
    public async Task<string?> DeleteAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await GetAsync(versionId, cancellationToken);
        var blockers = new DeleteBlockers($"Model version v{version.VersionNumber}");
        blockers.AddNamed("read by report versions", await ReportReadersAsync([versionId], cancellationToken));
        var conflict = blockers.Detail();
        if (conflict is not null) return conflict;

        var model = await models.GetAsync(new { version.ModelId }, cancellationToken);
        if (model.CurrentModelVersionId == versionId) await models.UpdateAsync(model with { CurrentModelVersionId = null }, cancellationToken);
        await RemoveAsync([versionId], cancellationToken);
        return null;
    }

    /// <summary>Hard-deletes draft versions: their content first, then the versions.</summary>
    /// <param name="versionIds">The draft versions.</param>
    /// <param name="cancellationToken">Cancels the remaining deletes.</param>
    /// <returns>A task that completes when the versions are deleted.</returns>
    public async Task RemoveAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        await remover.RemoveContentAsync(versionIds, cancellationToken);
        await versions.DeleteAsync(new { ModelVersionId = versionIds }, cancellationToken);
    }

    /// <summary>The report versions that read any of the model versions.</summary>
    /// <param name="versionIds">Model versions.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One name per report version.</returns>
    public Task<IEnumerable<string>> ReportReadersAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        return reports.ModelVersionReadersAsync(versionIds, cancellationToken);
    }
}
