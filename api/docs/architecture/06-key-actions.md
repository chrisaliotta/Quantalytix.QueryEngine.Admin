# Key actions

[Index](README.md) · [High-level architecture](01-architecture.md) · [Request pipeline](02-request-pipeline.md) ·
[Core classes](03-core-classes.md) · [Snapshot lifecycle](04-version-lifecycle.md) · [Feature classes](05-feature-classes.md)

Each sequence starts after authentication, the route existence check and input validation
([Request pipeline](02-request-pipeline.md)). Paths are relative to `/api/tenants/{tenantId}`.

## Dimension commit

The user adds a member and moves another in the dimension editor, then saves. Each edit already went through
`POST dimensions/{id}/changes/preview`; Save posts the same operations to `changes`.

```mermaid
sequenceDiagram
    participant Ctl as DimensionsController
    participant Svc as DimensionChanges
    participant P as ChangeSetPipeline
    participant Agg as DimensionAggregate
    participant Rules as Snapshot rules
    participant Plan as WritePlan
    participant Rev as SemanticRevision
    Ctl->>Svc: CommitAsync(dimensionId, operations)
    Svc->>Svc: refuse a report dimension (409)
    Svc->>P: CommitAsync
    P->>Agg: LoadAsync (dimension, roles, live version, levels, members, bands, surroundings)
    loop each operation
        P->>P: validators (400 under operations[n])
        P->>Agg: Apply (saveMember resolves tmp ids, moveMember)
    end
    P->>Rules: Gate rules (keys unique, parents in version, acyclic, consumers resolve)
    P->>Rules: Commit rules (none for dimensions)
    P->>Agg: Plan(before, after)
    Agg->>Plan: Sync members; delete and insert qe_dimension_closure
    P->>Plan: Ordered() then execute
    P->>Agg: IsEngineVisible: live content changed
    P->>Rev: AdvanceAsync
    P->>Agg: LoadAsync, ReadAsync with stored ids
    P-->>Ctl: state and idMap { "tmp:...": "123..." }
```

The commit replays the operations onto the dimension as stored now. A refusal at any step returns its problem and nothing
is written. Because the version is live, the change reaches the next query once the revision advances.

## Dataset commit

The live version of a dataset gets a new field that creates a column on its managed table.

```mermaid
sequenceDiagram
    participant Ctl as DatasetVersionsController
    participant Ch as DatasetChanges
    participant P as ChangeSetPipeline
    participant Agg as DatasetAggregate
    participant Rules as Snapshot rules
    participant Ddl as DatasetSchemaChanges
    participant Rev as SemanticRevision
    participant Mat as DatasetMaterializations
    Ctl->>Ch: CommitAsync(versionId, [saveField with createColumn])
    Ch->>P: CommitAsync
    P->>Agg: Load the live version, Apply
    P->>Rules: Gate and Commit rules (fields, bindings resolvable in live, enabled reports that read the dataset)
    Rules-->>P: violations (422 when any)
    P->>Agg: Plan(before, after)
    Agg->>Ddl: Plan(before, after)
    Ddl-->>Agg: plan.Schema: rename table, ADD COLUMN, redefine materialized view
    P->>P: execute: schema statements first, then field rows, root last
    P->>Rev: AdvanceAsync
    Ch->>Mat: MaterializeAsync for JSON path fields that asked for it
    Ch-->>Ctl: 200 state and idMap
```

Schema statements run before every row write because DDL cannot be rolled back; `DatasetSchemaChanges` derives them from
the difference between the stored and changed physical catalog, so a replay after a partial commit plans none. The DDL never
renames or drops a table or column that a frozen snapshot's dataset version reads. Because the version is live, the change
reaches the next query once the revision advances.

## Snapshot capture

```mermaid
sequenceDiagram
    participant Ctl as SnapshotsController
    participant Cmd as SnapshotCommands
    participant Info as qe_database_info
    participant Cp as IVersionCopier (per kind)
    participant Rm as IVersionRemover (per kind)
    participant Snap as qe_snapshot
    participant Rev as SemanticRevision
    Ctl->>Cmd: CaptureAsync(key, displayName, description)
    Cmd->>Info: read semantic_revision
    loop each kind, each live version
        Cmd->>Cp: CopyAsync(liveVersionId, newSnapshotId, null)
    end
    Cmd->>Snap: insert the frozen snapshot row
    Cmd->>Info: read semantic_revision again
    alt revision changed
        Cmd->>Rm: RemoveVersionAsync for every copy
        Cmd-->>Ctl: 409
    else unchanged
        Cmd->>Rev: AdvanceAsync
        Cmd-->>Ctl: 201 snapshot
    end
```

The copies are written before the snapshot row, so a stop midway leaves versions no snapshot row names, which no request can
reach. The live versions are only read; their ids never change.

## Report commit with report bridges

The user adds a bridge source to a report, maps source items to rows, and saves.

```mermaid
sequenceDiagram
    participant Ctl as ReportsController
    participant P as ChangeSetPipeline
    participant Agg as ReportAggregate
    participant Src as ReportSourceWrites
    participant Br as ReportBridges
    participant Plan as WritePlan
    Ctl->>P: CommitAsync(reportId, [saveSource, setRowRules, ...])
    P->>Agg: Load, Apply
    P->>P: ReportGateRule (an enabled report must pass every report rule)
    P->>Agg: Plan(before, after)
    Agg->>Plan: Sync content rows (members, lines, columns, profiles, ...)
    Agg->>Src: Plan(before, after, depth)
    Src->>Plan: Call(Insert): Br.CreateAsync, then insert the source row with its bridge id
    Src->>Plan: Call(Insert): Br.ReplaceRowAsync per row whose rules changed
    Agg->>Plan: closure of the report dimension when members changed; report row last
    P->>Plan: execute in order
    P->>P: advance the revision when the report is enabled
    P-->>Ctl: 200 ReportDocument
```

Report bridges are written only through the bridges module's public `ReportBridges` service. `WritePlan.Call` places each
call where a row write of its kind and depth would run: a new source's bridge is created before its row, a renamed source
renames its bridge, a deleted source's row is deleted before its bridge (`ReportBridges.DeleteAsync`, which removes the
version and rules), and rules into a row are replaced after the sources that own them. A commit that stops midway leaves at
most a bridge no source owns, which the engine never reads. The preview of the same operations also returns the report's
diagnostics; for a direct source, `ReportDiagnosticsBuilder` reads the field's distinct values through `DatasetFieldValues`
and `ISourceObjectCatalog.ListDistinctValuesAsync` to report the values that name no data row.

## Snapshot restore

```mermaid
sequenceDiagram
    participant Ctl as SnapshotsController
    participant Cmd as SnapshotCommands
    participant Rules as IVersionRules (per kind)
    participant Cp as IVersionCopier (per kind)
    participant Rm as IVersionRemover (per kind)
    participant Log as qe_snapshot_restore
    participant Rev as SemanticRevision
    Ctl->>Cmd: RestoreAsync(snapshotId)
    Cmd->>Cmd: the live snapshot itself is refused (409)
    loop each kind, each version the snapshot holds
        Cmd->>Rules: EvaluateAsync(frozenVersionId)
    end
    Rules-->>Cmd: violations (422 when any, nothing written)
    loop each identity the snapshot holds
        Cmd->>Cp: CopyAsync(frozenVersionId, liveSnapshotId, liveVersionId or null)
    end
    loop each live identity the snapshot lacks
        Cmd->>Rm: RemoveVersionAsync(liveVersionId), RemoveIdentityAsync when no snapshot holds it
    end
    Cmd->>Log: insert the restore with restored_by
    Cmd->>Rev: AdvanceAsync
    Cmd-->>Ctl: 200
```

Every rule runs before anything is written, so a snapshot whose versions no longer pass, for example because a physical table
drifted, is refused whole. A copy over a live version keeps the live version's id and replaces its content; for a dimension,
the copied closure rows come with it.

## Delete check and blocked delete

```mermaid
sequenceDiagram
    participant Client
    participant Ctl as DimensionsController
    participant Q as DimensionQueries
    participant Cmd as DimensionCommands
    participant R as DimensionDeleteRules
    Client->>Ctl: GET dimensions/{id}/delete-check
    Ctl->>Q: DimensionDeleteCheckAsync
    Q->>R: check over the snapshot and its usage
    R-->>Client: 200 { allowed: false, message, reasons: [{ code: referenced_by_mapping_set, ... }] }
    Client->>Ctl: DELETE dimensions/{id}
    Ctl->>Cmd: DeleteAsync
    Cmd->>R: same check
    Cmd-->>Client: 409 urn:qe:problem:delete-blocked with the same reasons as violations
```

When the check allows the delete, `DimensionCommands.DeleteAsync` removes the live version with its content, the roles and
the dimension row, and the controller answers 204. A frozen snapshot that holds a version of the dimension is a blocker
(`referenced_by_snapshot`). Deletes inside a change set (a member, level or role) run the same rules through
`context.Blocked` and answer the same 409.

A dataset delete runs the same way through `DatasetDeleteRules`, and builds a `WritePlan`: `DatasetTableDrops` adds, as schema
statements, the drop of each managed table that only the dataset's live version reads, after its materialized view, and the delete
of its `qe_managed_source_object` row; the version content, versions and dataset row follow children-first.
