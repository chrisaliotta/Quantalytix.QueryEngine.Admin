# Version lifecycle

[Diagram index](README.md) · [Architecture](01-architecture.md) · [Request pipeline](02-request-pipeline.md) · [Core classes](03-core-classes.md) · [Feature classes](05-feature-classes.md) · [Key actions](06-key-actions.md)

## Lifecycle states

The drawings show the transitions `VersionTransitions` allows for the six versioned kinds. Every status change goes through `PUT /api/tenants/{tenantId}/versions/{kind}/{id}/status`, which `VersionStatusController` hands to `VersionWorkflow`; any other pair of statuses is refused with 409 "Transition not allowed", so a version cannot skip validation, and `retired` is final.

The kinds come in two families. The strict kinds, `dataset_version` and `semantic_release`, are versioned for reproducibility: a published version never changes, several can be published at once for releases to pin, and one is retired by hand.

```mermaid
stateDiagram-v2
    [*] --> draft : create
    draft --> validated : validate
    validated --> draft : back to draft
    validated --> published : publish
    published --> retired : retire
```

The living kinds, `dimension_version`, `mapping_version`, `bridge_version` and `calculation_model_version`, hold the default content queries read. An identity has at most one published version, which stays editable; publishing the next version retires it, so a living version is never retired by hand. Reports are not versioned: a report is edited in place and enabled or disabled, and [the report gate](#the-report-gate) keeps an enabled report valid.

```mermaid
stateDiagram-v2
    [*] --> draft : create
    draft --> validated : validate
    validated --> draft : back to draft
    validated --> published : publish
    published --> retired : the next version of the identity is published
```

| Kinds | Transition | Before the status is written | After the status is written |
| --- | --- | --- | --- |
| All | draft to validated | `OnValidatingAsync` of each lifecycle handler of the kind, then every publication rule of the kind; any violation rejects the change with 422 | Nothing more |
| All | validated to draft | Nothing | Nothing more |
| All | validated to published | Nothing | `published_at` from `TimeProvider`, `OnPublishedAsync` of each handler of the kind, and `SemanticRevision.AdvanceAsync` for the living kinds |
| Strict | published to retired | Every retirement rule of the kind; any violation rejects with 422 | `published_at` is kept |

`VersionTransitions.RunsPublicationRules` is true for draft to validated; `RunsRetirementRules` is true for published to retired of a strict kind. `VersionKinds.Living` lists the living kinds.

Which versions accept content writes follows the families. `[RequireDraft]` guards the strict kinds' writes and every version delete, so only a draft changes or goes away; `[RequireEditable]` guards the living kinds' writes, which a draft or the published version accepts, and answers 409 "Version is not editable" for a validated or retired one. A retired living version is frozen, because a historical assumption set may still pin it.

**Sources:** [VersionTransitions.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/VersionTransitions.cs), [VersionWorkflow.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/VersionWorkflow.cs), [PublicationStatuses.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Vocabulary/PublicationStatuses.cs), [VersionKinds.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Vocabulary/VersionKinds.cs), [VersionStatusController.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Versioning/VersionStatusController.cs), [RequireDraftFilter.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Versioning/RequireDraftFilter.cs), [RequireEditableFilter.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Versioning/RequireEditableFilter.cs), [VersionWorkflowTests.cs](../../tests/Quantalytix.QueryEngine.Admin.Tests/Versioning/VersionWorkflowTests.cs).

## Leaving draft

The drawing shows draft to validated, the transition that derives data and runs the publication rules. It matters because leaving draft is when a version's content is checked before it reaches queries: publishing does not run the rules again.

```mermaid
sequenceDiagram
    autonumber
    actor Client as Web SPA
    participant Ctl as VersionStatusController
    participant Wf as VersionWorkflow
    participant Reg as VersionStoreRegistry
    participant Store as IVersionStore
    participant Tr as VersionTransitions
    participant Handlers as IVersionLifecycleHandler
    participant Rules as IPublicationRule
    Client->>Ctl: PUT versions/{kind}/{id}/status with status validated
    Note over Client,Ctl: ValidationFilter checks kind against version_kind and status against publication_status
    Ctl->>Wf: TransitionAsync(kind, id, validated)
    Wf->>Reg: Get(kind)
    Reg-->>Wf: the kind's store
    Wf->>Store: GetAsync(id)
    Store-->>Wf: VersionState
    Wf->>Tr: IsAllowed(kind, current status, validated)
    alt Not allowed, the version is not a draft
        Wf-->>Ctl: NotAllowed with the current state
        Ctl-->>Client: 409 Transition not allowed
    else Allowed
        Wf->>Tr: RunsPublicationRules(draft, validated)
        loop Each handler whose Kind matches, in registration order
            Wf->>Handlers: OnValidatingAsync(id)
        end
        loop Each publication rule whose Kind matches, in registration order
            Wf->>Rules: EvaluateAsync(id)
            Rules-->>Wf: violations, empty when the version passes
        end
        alt Any violation
            Wf-->>Ctl: Rejected with every violation, status unchanged
            Ctl-->>Client: 422 RuleViolationProblemDetails from RuleViolationResults
        else No violation
            Wf->>Store: SetStatusAsync(id, validated, publishedAt unchanged)
            Wf-->>Ctl: Applied with the new state
            Ctl-->>Client: 200 VersionState
        end
    end
```

Every rule of the kind runs, so the 422 lists all violations at once rather than the first. Handlers run before the rules and write even when a rule then rejects the change: `ClosureBuilder` rebuilds the closure of a dimension version every time it is validated. A living kind's rules check the version against what it will replace as well as on its own: a dimension version must keep every key live objects name (`dimension_consumers_resolve`), a calculation model version must serve every enabled report of its model (`report_metrics_resolve` and `profiles_complete` run over those reports), and a release must serve every enabled report's sources (`report_sources_resolve`); see [the report gate](#the-report-gate).

**Sources:** [VersionStatusController.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Versioning/VersionStatusController.cs), [VersionWorkflow.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/VersionWorkflow.cs), [VersionStoreRegistry.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/VersionStoreRegistry.cs), [VersionStore.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/VersionStore.cs), [VersionTransitions.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/VersionTransitions.cs), [IVersionLifecycleHandler.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/IVersionLifecycleHandler.cs), [IPublicationRule.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/IPublicationRule.cs), [RuleViolationResults.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Errors/RuleViolationResults.cs), [VersionReferenceValidator.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/VersionReferenceValidator.cs), [StatusChangeInputValidator.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/StatusChangeInputValidator.cs).

## Publishing

The drawing shows validated to published. No rules run; the workflow stamps the publication time, lets the kind's handlers cascade, and advances the semantic revision for a living kind, whose published version queries read without a release activation.

```mermaid
sequenceDiagram
    autonumber
    participant Ctl as VersionStatusController
    participant Wf as VersionWorkflow
    participant Store as IVersionStore
    participant Clock as TimeProvider
    participant Retirer as PredecessorRetirer
    participant Pinner as ActiveSetPinner
    participant Rev as SemanticRevision
    Ctl->>Wf: TransitionAsync(kind, id, published)
    Wf->>Store: GetAsync(id)
    Store-->>Wf: VersionState, status validated
    Note over Wf: IsAllowed is true and no rules run from validated to published
    Wf->>Clock: GetUtcNow()
    Clock-->>Wf: publishedAt
    Wf->>Store: SetStatusAsync(id, published, publishedAt)
    opt A living kind
        Wf->>Retirer: OnPublishedAsync(id)
        Retirer->>Store: PublishedSiblingIdsAsync(id)
        Retirer->>Store: SetStatusAsync(sibling, retired, the sibling's publishedAt)
        Note over Wf: The kind's feature handlers run next, such as ClosureBuilder, which does nothing on publish
        Wf->>Pinner: OnPublishedAsync(id)
        Pinner->>Store: IdentityIdAsync(id)
        Note over Pinner: Upserts the active set's pin of the identity, replacing the predecessor's
        Wf->>Rev: AdvanceAsync()
    end
    Wf-->>Ctl: Applied with status published and publishedAt
```

The status is written before the handlers run, so `PredecessorRetirer` finds the identity's other published version and retires it with its original publication time, and `ActiveSetPinner` repoints the active assumption set; historical sets keep pinning the version they captured, now retired. Publishing a dataset version or a semantic release changes nothing the engine reads until a release is activated, so it retires nothing and leaves the revision alone.

**Sources:** [VersionWorkflow.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/VersionWorkflow.cs), [VersionStore.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/VersionStore.cs), [LivingVersionStore.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/LivingVersionStore.cs), [PredecessorRetirer.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/PredecessorRetirer.cs), [ActiveSetPinner.cs](../../src/Quantalytix.QueryEngine.Admin.Application/AssumptionSets/ActiveSetPinner.cs), [SemanticRevision.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/SemanticRevision.cs), [VersionWorkflowTests.cs](../../tests/Quantalytix.QueryEngine.Admin.Tests/Versioning/VersionWorkflowTests.cs), [AssumptionSetTests.cs](../../tests/Quantalytix.QueryEngine.Admin.Tests/AssumptionSets/AssumptionSetTests.cs).

## Editing and checking a published version

The drawing shows a write to the published version of a living kind. It matters because the edit is live: the next query reads it, so no rules gate it, and the Validate check is how an author confirms the version still passes them.

```mermaid
sequenceDiagram
    autonumber
    actor Client as Web SPA
    participant Fields as ValidationFilter
    participant Filter as RequireEditableFilter
    participant Ctl as Feature controller
    participant Rev as SemanticRevision
    participant Status as VersionStatusController
    participant Wf as VersionWorkflow
    Client->>Fields: PUT or POST or DELETE content of the published version
    Fields->>Filter: the input passes its field rules, otherwise 400
    Filter->>Filter: GetAsync of the version's state; 409 unless draft or published
    Filter->>Ctl: the action
    Ctl-->>Filter: result
    opt The version is published and the result is 2xx without an exception
        Filter->>Rev: AdvanceAsync()
    end
    Filter-->>Client: result
    Client->>Status: POST versions/{kind}/{id}/validation
    Status->>Wf: CheckAsync(kind, id)
    Note over Wf: Every publication rule of the kind; nothing is written
    Wf-->>Status: violations
    Status-->>Client: 200 VersionCheck with violations, empty when the version passes
```

A failed write, a 400 from the field rules or a 409 from a delete blocker, changed nothing, so it keeps the revision. Edits to a draft never advance it, because nothing reads drafts. A report's dimension and its sources' bridges are living versions too, but `[RequireAnalytical]` refuses these generic writes and status changes to them with 409 "Owned by a report": the report editor writes them, under [the report gate](#the-report-gate).

**Sources:** [RequireEditableAttribute.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Versioning/RequireEditableAttribute.cs), [RequireEditableFilter.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Versioning/RequireEditableFilter.cs), [VersionStatusController.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Versioning/VersionStatusController.cs), [VersionWorkflow.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/VersionWorkflow.cs), [VersionCheck.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/Models/VersionCheck.cs), [RequireEditableFilterTests.cs](../../tests/Quantalytix.QueryEngine.Admin.Tests/Versioning/RequireEditableFilterTests.cs).

## The report gate

The drawing shows a write to an enabled report, `PUT reports/{reportId}/rows` from the report editor. Reports have no versions, so every write goes to the report the engine runs, and the report rules gate the write itself rather than a transition. It matters because an enabled report never breaks its rules: a write that would is refused, and nothing is written.

```mermaid
sequenceDiagram
    autonumber
    actor Client as Web SPA
    participant Filter as AdvancesReportRevisionFilter
    participant Ctl as ReportEditorController
    participant Svc as ReportRowService
    participant Changes as ReportChanges
    participant Reports as ReportRecord gateway
    participant Reader as ReportGraphReader
    participant Rules as ReportRules
    participant Rev as SemanticRevision
    Client->>Filter: PUT reports/{reportId}/rows
    Note over Client,Filter: ValidationFilter has checked the rows' shape and keys, otherwise 400
    Filter->>Reports: GetAsync(reportId), whether it is enabled
    Filter->>Ctl: the action
    Ctl->>Svc: ReplaceAsync(reportId, input)
    Note over Svc: Computes the members, overlay rows, member options and kept variable scopes in memory
    Svc->>Changes: ApplyAsync(reportId, change, write)
    Changes->>Reports: GetAsync(reportId)
    opt The report is enabled
        Changes->>Reader: ReadAsync(report)
        Reader-->>Changes: the stored ReportGraph
        Changes->>Rules: EvaluateAsync(the graph the change returns)
        Rules-->>Changes: every report rule's violations, in the active release and the model's published version
    end
    alt Any violation
        Changes-->>Svc: violations, nothing written
        Svc-->>Ctl: violations
        Ctl-->>Filter: 422 from ReportWriteResults.Rejected
    else No violation, or the report is disabled
        Changes->>Changes: write(): bulk upserts and deletes, then one closure rebuild
        Changes-->>Svc: no violations
        Ctl-->>Filter: 200 EditorView
        opt The report was or is enabled
            Filter->>Rev: AdvanceAsync()
        end
    end
    Filter-->>Client: result
```

`ReportChanges.ApplyAsync` takes the change as a function over the report's `ReportGraph`, so the rules see the report as the write would leave it, without a draft or a transaction. Each `IReportRule` is a pure `Evaluate(ReportGraph, ReportScope)`; `ReportRules` reads the scope once: the active release's versions of the sources' datasets and the templates of the calculation dimensions' roles. Every report write goes through it: the settings, rows, sources, source variables, profiles, calculation dimensions, variable scopes and columns. Two writes skip it. A source's bridge items (`PUT reports/{reportId}/sources/{sourceId}/items`) are checked by their validator against the report's rows and the source's dimension, and no report rule reads them. The identity attributes (`PUT reports/{reportId}`) are read by no report rule.

A disabled report takes any write. `PUT reports/{reportId}/status` with `isEnabled: true` checks the report as it stands and enables it only when every rule passes, answering 422 from `ReportWriteResults.NotEnabled` otherwise; disabling always succeeds. `POST reports/{reportId}/validation` runs the same check and changes nothing. `AdvancesReportRevisionFilter` advances the semantic revision after a successful write to a report that was enabled before it or is enabled after it, so enabling, disabling and deleting an enabled report advance it too.

The report rules also guard what enabled reports read. `ReportConsumerRule` adapts a report rule into a publication rule: `report_metrics_resolve` and `profiles_complete` run over every enabled report of a calculation model whose version is leaving draft, and `report_sources_resolve` over every enabled report when a release is leaving draft, against that release's dataset versions. A violation rejects the transition with 422 like any publication rule.

**Sources:** [ReportEditorController.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Reports/ReportEditorController.cs), [AdvancesReportRevisionFilter.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Reports/AdvancesReportRevisionFilter.cs), [ReportWriteResults.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Reports/ReportWriteResults.cs), [ReportsController.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Reports/ReportsController.cs), [ReportRowService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/ReportEditor/ReportRowService.cs), [ReportChanges.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Reports/ReportChanges.cs), [ReportRules.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Reports/ReportRules.cs), [IReportRule.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Reports/IReportRule.cs), [ReportGraphReader.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Reports/ReportGraphReader.cs), [ReportStatusService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Reports/ReportStatusService.cs), [ReportConsumerRule.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Reports/ReportConsumerRule.cs), [ReportGateTests.cs](../../tests/Quantalytix.QueryEngine.Admin.Tests/ReportEditor/ReportGateTests.cs), [AdvancesReportRevisionFilterTests.cs](../../tests/Quantalytix.QueryEngine.Admin.Tests/Reports/AdvancesReportRevisionFilterTests.cs).

## Retiring

The drawing shows published to retired of a strict kind. Retirement rules guard metadata that runtime queries still read; today the only one is `ReleaseNotActiveRule` (`release_not_active`) for `semantic_release`, which refuses to retire the release `qe_database_info.active_release_id` points at.

```mermaid
sequenceDiagram
    autonumber
    participant Ctl as VersionStatusController
    participant Wf as VersionWorkflow
    participant Store as IVersionStore
    participant Rules as IRetirementRule
    participant Info as DatabaseInfoRecord gateway
    Ctl->>Wf: TransitionAsync(kind, id, retired)
    Wf->>Store: GetAsync(id)
    Store-->>Wf: VersionState, status published
    Note over Wf: IsAllowed is false for a living kind, answered 409
    loop Each retirement rule whose Kind matches
        Wf->>Rules: EvaluateAsync(id)
        Note over Rules,Info: ReleaseNotActiveRule reads the active release ID
        Rules->>Info: GetAsync(SingletonId 1)
        Rules-->>Wf: release_not_active when id is the active release
    end
    alt Any violation
        Wf-->>Ctl: Rejected, answered 422, status unchanged
    else No violation
        Wf->>Store: SetStatusAsync(id, retired, publishedAt kept)
        Wf-->>Ctl: Applied with status retired
    end
```

Retiring the active release would leave every query without one, because the engine reads the active release among published rows only; another release has to be activated first.

**Sources:** [VersionWorkflow.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/VersionWorkflow.cs), [IRetirementRule.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/IRetirementRule.cs), [ReleaseNotActiveRule.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Releases/ReleaseNotActiveRule.cs), [ReleasesAndSystemFeatureServiceCollectionExtensions.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Configuration/ReleasesAndSystemFeatureServiceCollectionExtensions.cs), [ReleaseRetirementTests.cs](../../tests/Quantalytix.QueryEngine.Admin.Tests/Releases/ReleaseRetirementTests.cs).

## Semantic revision

The engine keys its metadata cache by `qe_database_info.semantic_revision`, so the API advances it whenever rows the engine may have cached change outside a release activation. The table lists every place the code calls `SemanticRevision.AdvanceAsync`.

| Trigger | Code | What changed |
| --- | --- | --- |
| Publishing a living kind | `VersionWorkflow`, for `VersionKinds.Living`, once per transition | The published version, which the active assumption set now pins, and its retired predecessor |
| Editing the published version of a living kind | `RequireEditableFilter`, after a successful write | Content queries read now |
| Capturing or deleting an assumption set | `AssumptionSetService.CaptureAsync` and `DeleteAsync` | The set keys requests can name |
| Activating a release | `ReleaseActivator.ActivateAsync`, after it writes `active_release_id` | The release every query runs on |
| Editing a dataset | `DatasetService.UpdateAsync`, always | `qe_dataset.display_name` and `is_enabled` |
| Editing a dimension | `DimensionService.UpdateAsync`, when `display_name` or `is_enabled` changed | `qe_dimension` identity |
| Editing a dimension role | `DimensionRoleService.UpdateAsync`, when the row changed | `qe_dimension_role.display_name` and `description` |
| Editing a mapping set | `MappingSetService.UpdateAsync`, when the row changed | `qe_mapping_set.source_namespace` and `display_name` |
| Editing a bridge | `BridgeService.UpdateAsync`, when the row changed | `qe_bridge.display_name` and `is_enabled` |
| Writing to a report | `AdvancesReportRevisionFilter`, after a successful write to a report enabled before or after it | Anything the engine caches with the report's definition, including whether it is enabled at all |

Publishing or retiring a `dataset_version` or `semantic_release` never advances it: these reach queries only through the release that pins them, and activating a release advances it in `ReleaseActivator`. Relabelling an assumption set does not advance it either, because the engine reads only its key and pins.

**Sources:** [SemanticRevision.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/SemanticRevision.cs), [VersionWorkflow.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/VersionWorkflow.cs), [RequireEditableFilter.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Versioning/RequireEditableFilter.cs), [AssumptionSetService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/AssumptionSets/AssumptionSetService.cs), [ReleaseActivator.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Releases/ReleaseActivator.cs), [DatasetService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Datasets/DatasetService.cs), [DimensionService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Dimensions/DimensionService.cs), [MappingSetService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Mappings/MappingSetService.cs), [BridgeService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Bridges/BridgeService.cs), [DimensionRoleService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Dimensions/DimensionRoleService.cs), [AdvancesReportRevisionFilter.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Reports/AdvancesReportRevisionFilter.cs).

## Lifecycle handlers

The table lists the `IVersionLifecycleHandler` implementations registered today. The workflow calls each handler of a version's kind in registration order: `OnValidatingAsync` before the publication rules, `OnPublishedAsync` after the published status is written.

| Handler | Kind | `OnValidatingAsync` | `OnPublishedAsync` | Registered in |
| --- | --- | --- | --- | --- |
| `PredecessorRetirer` | Each living kind | Nothing | Retires the identity's other published version, keeping its `published_at` | `AddAdminApplication`, one per kind |
| `ClosureBuilder` | `dimension_version` | Lists the version's members, deletes its `qe_dimension_closure` rows, and inserts one row per ancestor and descendant pair, depth 0 included; members in or below a parent cycle get none, and `hierarchy_acyclic` reports the cycle. `DimensionMemberService` calls the same rebuild after every member write, so a live edit keeps the published closure current | Nothing | `AddDimensionsFeature` |
| `ActiveSetPinner` | Each living kind | Nothing | Pins the version in the active assumption set, replacing its identity's pin | `AddReleasesAndSystemFeature`, one per kind |

**Sources:** [PredecessorRetirer.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/PredecessorRetirer.cs), [ClosureBuilder.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Dimensions/ClosureBuilder.cs), [DimensionMemberService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Dimensions/DimensionMemberService.cs), [ActiveSetPinner.cs](../../src/Quantalytix.QueryEngine.Admin.Application/AssumptionSets/ActiveSetPinner.cs), [AdminApplicationServiceCollectionExtensions.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Configuration/AdminApplicationServiceCollectionExtensions.cs), [DimensionsFeatureServiceCollectionExtensions.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Configuration/DimensionsFeatureServiceCollectionExtensions.cs), [ReleasesAndSystemFeatureServiceCollectionExtensions.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Configuration/ReleasesAndSystemFeatureServiceCollectionExtensions.cs), [VersionWorkflow.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/VersionWorkflow.cs).
