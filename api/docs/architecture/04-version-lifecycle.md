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
| Reaches queries | At once: a request that names no snapshot reads it, and every engine-visible commit advances the semantic revision | When a query, catalog request or report comparison names its key |
| Validated | Every commit runs every gate and commit rule; a violation refuses the commit with 422 and writes nothing | Not again: it is a copy of a live snapshot that passed every rule |
| Relabel, delete | Neither | Both |

`LiveSnapshot` (`Application/Snapshots`) reads the live snapshot's id once per request. Every aggregate loader resolves an
identity's version through it, so a change set always edits the live version and an identity's id is all a route names.

## Validation on commit

`RuleStage` has two stages, `Gate` and `Commit`. A gate refuses a change set in preview and commit alike. A commit rule's
violations are findings in a preview, which the editor flags, and refuse the commit with 422. Every rule runs on every
commit, so live is always valid and capture does not validate.

Datasets follow the same rule as every other kind. `CreateAsync` inserts the dataset's live version; its change set edits that
version, and its commit rules include the checks against the rest of live: `BoundDimensionsResolvableRule`,
`MappedBindingsResolvableRule`, `BridgeBindingsResolvableRule`, `BindingIdentityRule`, and the report checks of the enabled
reports that read the dataset. Materializing a JSON path field (`fields/{id}/materialization`) does not change what the version
means, so it runs no commit rules, but it advances the revision because the engine reads `qe_field.materialized_column`.

## Capture

`SnapshotCommands.CaptureAsync` copies every live version of every kind into a new frozen snapshot:

1. It reads `semantic_revision`.
2. It copies each live version, changed or not, through the kind's `IVersionCopier` into the new snapshot, in `SnapshotKinds`
   order. A copier remaps every internal id of the copied content through one `IdRemap`, so references between copied rows point
   at the copies; references to identities keep their ids. `ReportVersionCopier` remaps line, column, source and profile ids.
3. It writes the `qe_snapshot` row last, with `captured_at` and `captured_by`, the caller's token subject.
4. It reads `semantic_revision` again. When it changed, a commit ran during the capture, so the capture deletes its copies through
   each kind's `IVersionRemover` and refuses with 409.

The live version ids never change. A capture advances the semantic revision. `SnapshotQueries` answers the capture preview, the
count of versions per kind a capture would copy, without writing anything.

## Restore

`SnapshotCommands.RestoreAsync` makes live equal a frozen snapshot:

1. Every `IVersionRules` of every kind runs on the snapshot's versions, resolving what they name in the snapshot, which is what
   live would hold. Any violation refuses the restore with 422 and nothing changes; a physical table that drifted since the
   capture is the usual cause.
2. For each identity the snapshot holds, the kind's copier copies the frozen version over the identity's live version, passing the
   live version's id as `targetVersionId`, so the live id is kept and its content replaced. An identity with no live version gets a
   new one.
3. For each identity the snapshot lacks, the kind's remover deletes its live version, and deletes the identity row when no other
   snapshot holds a version of it.
4. It logs a `qe_snapshot_restore` row with `restored_by` and advances the semantic revision.

## Relabel, delete and compare

`RelabelAsync` changes a frozen snapshot's key, display name and description, and refuses the live snapshot. `DeleteAsync`
removes every version of a frozen snapshot with its content, through each kind's `IVersionRemover`, then the `qe_snapshot` row,
and refuses the live snapshot. Both advance the semantic revision, because a request names a snapshot by key.

`SnapshotQueries` lists snapshots, reads one with the version it holds of each identity, and compares two snapshots kind by kind,
listing the identities only one holds and those whose versions differ.

A delete of any identity is refused while a frozen snapshot holds a version of it, for all six kinds, and managed-table DDL refuses
to rename or drop a table or column that any dataset version, live or frozen, reads.

## Semantic revision

`SemanticRevision.AdvanceAsync` adds one to `qe_database_info.semantic_revision`, which the engine's metadata cache keys on.
It advances once per commit or command that changes what the engine reads:

| Advances | Does not advance |
|---|---|
| A commit that changes engine-visible live content, creating or deleting an identity included | A commit that changes only what the engine never reads, such as a disabled report |
| Enabling or disabling a dataset, bridge or report | A preview |
| Materializing or dematerializing a JSON path field | Refreshing a materialized field's view |
| Capturing, restoring, relabelling or deleting a snapshot | A refused capture, restore or commit |

Writes that must be seen together write their rows first and advance the revision last, so the engine never caches
half-written metadata under the new revision.
