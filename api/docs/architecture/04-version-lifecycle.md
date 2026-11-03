# Snapshot lifecycle

[Index](README.md) · [High-level architecture](01-architecture.md) · [Request pipeline](02-request-pipeline.md) ·
[Core classes](03-core-classes.md) · [Feature classes](05-feature-classes.md) · [Key actions](06-key-actions.md)

Six kinds are versioned (`SnapshotKinds`): `dataset`, `dimension`, `mapping_set`, `bridge`, `calculation_model` and
`report`. A report's own dimension and the bridges into it are dimensions and bridges like any other. Every version belongs to
exactly one snapshot (`qe_snapshot`), and an identity has at most one version per snapshot. Versions carry no status: whether a
version can be edited is decided by its snapshot.

## Live and frozen snapshots

| | Live snapshot | Frozen snapshot |
|---|---|---|
| How many | Exactly one, with key `live` | Any number, each with its own key |
| Edited | Every version in place, through the identity's change set | Never; its versions are read-only |
| Reaches queries | At once: a request that names no snapshot reads it, and every commit advances the semantic revision | When a query, catalog request or report comparison names its key |
| Validated | Every commit runs every gate and commit rule; a violation refuses the commit with 422 and writes nothing | Not again: it is a copy of a live snapshot that passed every rule |
| Relabel, delete, restore | None (409) | All three; a restore runs every rule against the database as it is now first |

`LiveSnapshot` (`Application/Snapshots`) reads the live snapshot's id once per request. Every aggregate loader resolves an
identity's version through it, so a change set always edits the live version and an identity's id is all a route names.

## Validation on commit

`RuleStage` has two stages, `Gate` and `Commit`, and disabled reports and models get no exemption: a disabled report with
findings cannot be saved. A gate refuses a change set in preview and commit alike. A commit rule's
violations are findings in a preview, which the editor flags, and refuse the commit with 422. Every rule runs on every
commit, so live is always valid and capture does not validate.

Datasets follow the same rule as every other kind. `CreateAsync` inserts the dataset's live version; its change set edits that
version, and its commit rules include the checks against the rest of live: the three binding identity rules
(`BoundDimensionsResolvableRule`, `MappedBindingsResolvableRule` and `BridgeBindingsResolvableRule`, on the base
`BindingIdentityRule`), and `DatasetReportsResolveRule`, which runs `report_sources_resolve` and `report_factors_apply` for
the enabled reports that read the dataset. Materializing a JSON path field (`fields/{id}/materialization`) does not change what the version
means, so it runs no commit rules, but it advances the revision because the engine reads `qe_field.materialized_column`.

## Capture

`SnapshotCommands.CaptureAsync` copies every live version of every kind into a new frozen snapshot:

1. A key another snapshot already uses is refused with 409 "Key taken".
2. It reads `semantic_revision`.
3. It copies each live version that `SnapshotIndex` lists, changed or not, through the kind's `IVersionCopier` into the new
   snapshot, in `SnapshotKinds` order. A copier remaps every internal id of the copied content through one `IdRemap`, so
   references between copied rows point at the copies; references to identities keep their ids. `ReportVersionCopier` remaps
   line, column, source and profile ids.
4. It reads `semantic_revision` again. When it changed, a commit ran during the capture and may have been copied half, so the
   capture deletes its copies through each kind's `IVersionRemover`, newest first, and refuses with 409 "Live changed during the
   capture".
5. It writes the `qe_snapshot` row last, with `captured_at` and `captured_by`, the caller's token subject, so a stop leaves copies
   no request can name rather than a snapshot missing versions, and advances the semantic revision.

The live version ids never change. `SnapshotQueries` answers the capture preview, the `SnapshotCounts` of live versions a capture
would copy, without writing anything.

## Restore

`SnapshotCommands.RestoreAsync` makes live equal a frozen snapshot:

1. The live snapshot is refused with 409. Every `IVersionRules` of every kind runs on the snapshot's versions, resolving what
   they name in the snapshot, as live would hold them, against the database as it is now. Any violation refuses the restore with
   422 and nothing changes; a physical table that drifted since the capture is the usual cause.
2. For each identity the snapshot holds, the kind's copier copies the frozen version over the identity's live version, passing the
   live version's id as `targetVersionId`, so the live id is kept and its content replaced. An identity with no live version gets a
   new one.
3. For each identity the snapshot lacks, the kind's remover deletes its live version, and deletes the identity row when no frozen
   snapshot holds a version of it.
4. It logs a `qe_snapshot_restore` row with `restored_by`, advances the semantic revision, and answers the new `TenantInfo`.

## Relabel, delete and compare

`RelabelAsync` changes a frozen snapshot's display name and description; the key and the versions never change, so it does not
advance the semantic revision. `DeleteAsync` deletes the `qe_snapshot` row first, so a stop leaves versions no request can name,
then the restore log and every version the snapshot holds with its content, through each kind's `IVersionRemover`, and each
identity no other snapshot holds; it advances the semantic revision. Both refuse the live snapshot with 409.

`SnapshotQueries` answers the `SnapshotList` (the live snapshot and the frozen ones, newest capture first, each with its
`SnapshotCounts` and last restore), a `SnapshotDetail` (the snapshot, one `SnapshotEntry` per identity it holds, and its
restores), and the `SnapshotComparison` of two snapshots: `added` and `removed`, the entries only one side holds, and `common`,
one `SnapshotPair` per identity both hold with both version ids and `differs`, whether the engine-visible content differs,
compared by keys rather than surrogate ids. An entry carries `dimensionId` for a mapping set (its dimension) or a bridge (its
source dimension), and `reportId` for a report dimension or report bridge (its owning report). A frozen version is read through
its kind's version route: `dataset-versions`, `dimension-versions`, `mapping-versions`, `bridge-versions`,
`calculation-model-versions` or `report-versions`.

A delete of any identity is refused while a frozen snapshot holds a version of it (`referenced_by_snapshot`, from
`SnapshotHolders`), for all six kinds, and renaming a managed table is refused with 409 while any other dataset
version, live or frozen, reads it. A dataset change set or materialization aimed at a frozen version is refused with 409 "Version
is frozen" (`DatasetRefusals.FrozenTitle`); dimensions, mapping sets, bridges, models and reports are edited through change sets on
the identity, which always target the live version, so their frozen versions have no edit route at all.

## Semantic revision

`SemanticRevision.AdvanceAsync` adds one to `qe_database_info.semantic_revision`, which the engine's metadata cache keys on.
`ChangeSetPipeline` advances it on every commit, and each command that writes metadata advances it once:

| Advances | Does not advance |
|---|---|
| Every change-set commit, a disabled report's or model's included | A preview |
| Creating or deleting an identity; enabling or disabling a report when the flag changes | Relabelling a snapshot |
| Materializing or dematerializing a JSON path field | Refreshing a materialized field's view |
| Capturing, restoring or deleting a snapshot | A refused capture, restore or commit |

Writes that must be seen together write their rows first and advance the revision last, so the engine never caches
half-written metadata under the new revision.
