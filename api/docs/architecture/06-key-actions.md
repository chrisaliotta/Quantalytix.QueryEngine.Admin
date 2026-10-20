# Key actions

[Diagram index](README.md) · [Architecture](01-architecture.md) · [Request pipeline](02-request-pipeline.md) · [Core classes](03-core-classes.md) · [Version lifecycle](04-version-lifecycle.md) · [Feature classes](05-feature-classes.md)

Each drawing traces one write from its controller to the table gateways, in the order the statements run. StarRocks runs no `UPDATE` or `DELETE` inside a transaction, so every gateway call is its own statement and nothing rolls back; services therefore write parents before children and delete children before parents. A participant named after a record, such as "DatasetRecord gateway", is the `ITableGateway<TRecord>` of that record, implemented by `StarRocksTableGateway`. Authentication, authorization and binding run first on every request, as [Request pipeline](02-request-pipeline.md) shows; the drawings mention validation and the draft check only where they matter.

**Sources:** [ITableGateway.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Metadata/ITableGateway.cs), [StarRocksTableGateway.cs](../../src/Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks/Tables/StarRocksTableGateway.cs), [DatasetVersionContentStore.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Datasets/DatasetVersionContentStore.cs).

## Creating a dataset and its draft v1

The drawing shows `POST /api/tenants/{tenantId}/datasets`. The dataset row is written before its first version, so the version never points at a missing dataset.

```mermaid
sequenceDiagram
    autonumber
    actor Client as Web SPA
    participant Filter as ValidationFilter
    participant Ctl as DatasetsController
    participant Svc as DatasetService
    participant Ids as IIdGenerator
    participant Datasets as DatasetRecord gateway
    participant VersionSvc as DatasetVersionService
    participant Versions as DatasetVersionRecord gateway
    Client->>Filter: POST datasets with DatasetInput
    Filter->>Filter: DatasetInputValidator, key unused and source object present
    Filter->>Ctl: Create(tenantId, input)
    Ctl->>Svc: CreateAsync(input)
    Svc->>Ids: NextId()
    Svc->>Datasets: InsertAsync(dataset)
    Svc->>VersionSvc: CreateAsync(datasetId, source object of version 1)
    VersionSvc->>Versions: ListAsync(DatasetId)
    Versions-->>VersionSvc: no versions yet
    Note over VersionSvc: VersionNumbers.Next gives 1
    VersionSvc->>Ids: NextId()
    VersionSvc->>Versions: InsertAsync(draft v1)
    Svc-->>Ctl: DatasetRecord
    Ctl-->>Client: 200 DatasetRecord
```

Version 1 is a draft over the source object the input names, supports aggregate and detail queries, and uses expression language 1.0. It has no fields, grains, bindings or measures until they are added.

**Sources:** [DatasetsController.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Datasets/DatasetsController.cs), [DatasetService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Datasets/DatasetService.cs), [DatasetVersionService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Datasets/DatasetVersionService.cs), [DatasetInputValidator.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Datasets/DatasetInputValidator.cs), [VersionNumbers.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/VersionNumbers.cs), [IdGenerator.cs](../../src/Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks/Identifiers/IdGenerator.cs).

## Cloning a dataset version

The drawing shows `POST .../datasets/{datasetId}/versions` with `cloneFromVersionId`. One `IdRemap` gives every ID inside the version one new ID, so references between the copies point at the copies, and the content is written parent table first.

```mermaid
sequenceDiagram
    autonumber
    actor Client as Web SPA
    participant Ctl as DatasetsController
    participant Svc as DatasetVersionService
    participant Cloner as DatasetVersionCloner
    participant Remap as IdRemap
    participant Versions as DatasetVersionRecord gateway
    participant Content as DatasetVersionContentStore
    participant Tables as Content gateways
    Client->>Ctl: POST datasets/{datasetId}/versions with cloneFromVersionId
    Note over Client,Ctl: DatasetVersionInputValidator requires the source to be a version of the dataset in the route
    Ctl->>Svc: CreateAsync(datasetId, input)
    Svc->>Versions: ListAsync(DatasetId)
    Note over Svc: VersionNumbers.Next of the existing version numbers
    Svc->>Cloner: CloneAsync(sourceVersionId, datasetId, versionNumber)
    Cloner->>Remap: new IdRemap(ids)
    Cloner->>Versions: GetAsync(source DatasetVersionId)
    Cloner->>Content: LoadAsync(source version)
    Content->>Tables: ListAsync of grains, bindings and measures, then fields, grain fields, time bindings, components, dependencies and time policies
    Content-->>Cloner: DatasetVersionContent
    Cloner->>Remap: Map(DatasetVersionId), MapOptional(DefaultTimeBindingId)
    Cloner->>Versions: InsertAsync(clone as draft, PublishedAt null)
    Cloner->>Remap: Map and MapOptional for every content ID and internal reference
    Cloner->>Content: InsertAsync(remapped content)
    Content->>Tables: fields.InsertManyAsync
    Content->>Tables: grains.InsertManyAsync
    Content->>Tables: grainFields.InsertManyAsync
    Content->>Tables: bindings.InsertManyAsync
    Content->>Tables: timeBindings.InsertManyAsync
    Content->>Tables: measures.InsertManyAsync
    Content->>Tables: components.InsertManyAsync
    Content->>Tables: dependencies.InsertManyAsync
    Content->>Tables: timePolicies.InsertManyAsync
    Cloner-->>Svc: new draft
    Svc-->>Ctl: DatasetVersionRecord
    Ctl-->>Client: 200 new draft version
```

The clone keeps the source's settings and source object. Because one `IdRemap` serves the whole clone, the new `DefaultTimeBindingId` and every binding's, component's and time policy's references land on the copied rows; references to other aggregates (dimensions, their versions, levels and members, mapping sets, bridge versions) keep their IDs. `InsertManyAsync` writes nothing for an empty list.

**Sources:** [DatasetsController.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Datasets/DatasetsController.cs), [DatasetVersionService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Datasets/DatasetVersionService.cs), [DatasetVersionCloner.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Datasets/DatasetVersionCloner.cs), [DatasetVersionContentStore.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Datasets/DatasetVersionContentStore.cs), [DatasetVersionContent.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Datasets/Models/DatasetVersionContent.cs), [IdRemap.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/IdRemap.cs), [DatasetVersionInputValidator.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Datasets/DatasetVersionInputValidator.cs), [DatasetVersionClonerTests.cs](../../tests/Quantalytix.QueryEngine.Admin.Tests/Datasets/DatasetVersionClonerTests.cs).

## Materializing a json_path field

The drawing shows `POST .../dataset-versions/{versionId}/fields/{fieldId}/materialize`, the one write that changes a source table. StarRocks applies the column change as an asynchronous job, so the executor waits for it before the field points at the new column.

```mermaid
sequenceDiagram
    autonumber
    actor Client as Web SPA
    participant Ctl as FieldsController
    participant Svc as MaterializedColumnService
    participant Fields as FieldRecord gateway
    participant Versions as DatasetVersionRecord gateway
    participant Sources as SourceObjectService
    participant Ddl as MaterializedColumnDdl
    participant Exec as SchemaChangeExecutor
    participant Db as Tenant database
    Client->>Ctl: POST fields/{fieldId}/materialize with columnName
    Note over Client,Ctl: FieldReferenceValidator and MaterializeInputValidator, then RequireDraftFilter
    Ctl->>Svc: MaterializeAsync(reference, input)
    Svc->>Fields: GetAsync(FieldId)
    Svc->>Versions: GetAsync(DatasetVersionId)
    opt A json_path field over a table
        Svc->>Sources: ListColumnsAsync(source table)
        Sources-->>Svc: columns, to check the name is free
    end
    alt Not a json_path field, a view, or the column exists
        Svc-->>Ctl: refusal
        Ctl-->>Client: 409 Field cannot be materialized
    else The column can be added
        Svc->>Ddl: AddColumn(table, columnName, field)
        Ddl-->>Svc: ALTER TABLE ADD COLUMN AS get_json_string, cast to the field type
        Svc->>Exec: ApplyAsync(table, statement)
        Exec->>Db: ALTER TABLE on a tenant connection
        loop Until State is FINISHED, one second apart
            Exec->>Db: SHOW ALTER TABLE COLUMN, newest job of the table
            Db-->>Exec: SchemaChangeJob with State and Msg
        end
        Note over Exec,Db: CANCELLED throws, and UseExceptionHandler answers 500
        Svc->>Fields: UpdateAsync(field with MaterializedColumn)
        Ctl-->>Client: 204
    end
```

The generated column holds what the engine would compute for the field: `get_json_string` of the JSON column at the field's path, cast to the field's type unless it is a string. Every version that reads the table shares the column, but only this draft field points at it, so published versions are unaffected.

**Sources:** [FieldsController.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Datasets/FieldsController.cs), [MaterializedColumnService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Datasets/MaterializedColumnService.cs), [MaterializedColumnDdl.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Datasets/MaterializedColumnDdl.cs), [SourceObjectService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/SourceObjects/SourceObjectService.cs), [SchemaChangeExecutor.cs](../../src/Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks/SchemaChanges/SchemaChangeExecutor.cs), [SchemaChangeJob.cs](../../src/Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks/SchemaChanges/Models/SchemaChangeJob.cs), [MaterializeInputValidator.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Datasets/MaterializeInputValidator.cs), [MaterializedColumnDdlTests.cs](../../tests/Quantalytix.QueryEngine.Admin.Tests/Datasets/MaterializedColumnDdlTests.cs).

## Bulk-replacing mapping entries

The drawing shows `PUT .../mapping-versions/{versionId}/entries`, which replaces a version's complete entry list. The whole list is validated before the old rows are deleted; on the published version the write is live, so it advances the semantic revision.

```mermaid
sequenceDiagram
    autonumber
    actor Client as Web SPA
    participant Filter as ValidationFilter
    participant Editable as RequireEditableFilter
    participant Ctl as MappingEntriesController
    participant Svc as MappingEntryService
    participant Ids as IIdGenerator
    participant Entries as DimensionMappingRecord gateway
    participant Rev as SemanticRevision
    Client->>Filter: PUT mapping-versions/{versionId}/entries with the complete list
    Filter->>Filter: MappingEntriesInputValidator, source keys and member keys fit and source keys are unique
    Filter->>Editable: valid
    Editable->>Ctl: the mapping_version is a draft or published
    Ctl->>Svc: ReplaceAsync(versionId, input)
    loop Each entry
        Svc->>Ids: NextId()
    end
    Svc->>Entries: DeleteAsync(MappingVersionId)
    Svc->>Entries: InsertManyAsync(rows), one INSERT per 500 rows
    Svc-->>Ctl: entries by source key
    Ctl-->>Editable: 200 entries under new IDs
    opt The version is published
        Editable->>Rev: AdvanceAsync()
    end
    Editable-->>Client: 200 entries under new IDs
```

`BridgeMappingService.ReplaceAsync` replaces a bridge version's mappings the same way, `DeleteAsync` by `BridgeVersionId` then `InsertManyAsync` under new mapping IDs. Whether the member keys name members of the published version of the set's dimension is checked by the `entry_member_in_version` and `entry_member_assignable` rules, when a draft is validated or when an author runs the Validate check of the published version.

**Sources:** [MappingEntriesController.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Mappings/MappingEntriesController.cs), [MappingEntryService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Mappings/MappingEntryService.cs), [MappingEntriesInputValidator.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Mappings/MappingEntriesInputValidator.cs), [RequireEditableFilter.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Versioning/RequireEditableFilter.cs), [BridgeMappingService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Bridges/BridgeMappingService.cs), [StarRocksTableGateway.cs](../../src/Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks/Tables/StarRocksTableGateway.cs).

## Saving a measure

The drawing shows `POST` and `PUT .../dataset-versions/{versionId}/measures`. The measure row comes first, then its components and time policy, and last the dependency rows of every formula of the version, derived from the formulas' text.

```mermaid
sequenceDiagram
    autonumber
    participant Ctl as MeasuresController
    participant Svc as MeasureService
    participant Measures as MeasureRecord gateway
    participant Components as MeasureComponentRecord gateway
    participant Policies as MeasureTimePolicyRecord gateway
    participant Deriver as MeasureDependencyDeriver
    participant Inspector as ExpressionInspector
    participant Dependencies as MeasureDependencyRecord gateway
    alt POST, a new measure
        Ctl->>Svc: CreateAsync(versionId, input)
        Svc->>Measures: InsertAsync(measure with a new ID)
    else PUT, an existing measure
        Ctl->>Svc: UpdateAsync(reference, input)
        Svc->>Measures: GetAsync(MeasureId)
        Svc->>Measures: UpdateAsync(measure, key kept)
        Svc->>Components: DeleteAsync(MeasureId)
        Svc->>Policies: DeleteAsync(MeasureId)
    end
    Svc->>Components: InsertManyAsync(components)
    opt The input has a time policy
        Svc->>Policies: InsertAsync(policy)
    end
    Svc->>Deriver: DeriveAsync(DatasetVersionId)
    Deriver->>Measures: ListAsync(DatasetVersionId)
    loop Each formula measure of the version
        Deriver->>Inspector: Inspect(ExpressionText)
        Inspector-->>Deriver: MeasureKeys
    end
    Note over Deriver: Each key becomes a row per measure of the version with that key, an unknown key gives none
    Deriver->>Dependencies: DeleteAsync(MeasureId in every measure of the version)
    Deriver->>Dependencies: InsertManyAsync(rows)
    Deriver-->>Svc: the version's dependency rows
    Svc-->>Ctl: MeasureDetail with this measure's dependencies
```

Every formula is derived again on every save, because a formula may name a measure created after it. `ExpressionInspector` parses with the engine's `ExpressionParser`, so the rows match what the engine will need; `formula_references_known_measures` and `formula_acyclic` check the result when the version is validated.

**Sources:** [MeasuresController.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Measures/MeasuresController.cs), [MeasureService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Measures/MeasureService.cs), [MeasureDependencyDeriver.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Measures/MeasureDependencyDeriver.cs), [ExpressionInspector.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Expressions/ExpressionInspector.cs), [MeasureDetail.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Measures/Models/MeasureDetail.cs), [MeasureDependencyDeriverTests.cs](../../tests/Quantalytix.QueryEngine.Admin.Tests/Measures/MeasureDependencyDeriverTests.cs).

## Validating and publishing a dimension version

The drawing shows two requests to the status endpoint for one `dimension_version`: validated, then published. Validation rebuilds the closure before the rules run, and the rules check that the version keeps every key live objects name; publishing retires the version it replaces, repoints the active assumption set and advances the semantic revision.

```mermaid
sequenceDiagram
    autonumber
    participant Ctl as VersionStatusController
    participant Wf as VersionWorkflow
    participant Store as DimensionVersionStore
    participant Closure as ClosureBuilder
    participant Rules as dimension_version rules
    participant Consumers as DimensionConsumers
    participant Retirer as PredecessorRetirer
    participant Pinner as ActiveSetPinner
    participant Rev as SemanticRevision
    Note over Ctl,Rev: First request, status validated
    Ctl->>Wf: TransitionAsync(dimension_version, id, validated)
    Wf->>Store: GetAsync(id)
    Wf->>Closure: OnValidatingAsync(id)
    Note over Closure: Deletes the version's closure rows and inserts a row per ancestor and descendant pair, depth 0 included
    loop The 11 dimension_version rules in registration order
        Wf->>Rules: EvaluateAsync(id)
    end
    Note over Rules,Consumers: dimension_consumers_resolve lists the keys live bindings, mapping entries, bridge mappings and reports name, and reports each one the version lacks
    Rules->>Consumers: ListAsync(DimensionId)
    Wf->>Store: SetStatusAsync(id, validated)
    Note over Ctl,Rev: Second request, status published
    Ctl->>Wf: TransitionAsync(dimension_version, id, published)
    Wf->>Store: GetAsync(id)
    Wf->>Store: SetStatusAsync(id, published, publishedAt)
    Wf->>Retirer: OnPublishedAsync(id)
    Retirer->>Store: PublishedSiblingIdsAsync(id), then SetStatusAsync(predecessor, retired, its publishedAt)
    Wf->>Closure: OnPublishedAsync(id), nothing to do
    Wf->>Pinner: OnPublishedAsync(id)
    Note over Pinner: Upserts the active set's pin of the dimension to the new version
    Wf->>Rev: AdvanceAsync()
```

Bindings, mapping entries, bridge mappings, report rows, member options and variable scopes name members by key, so none of them changes: from the revision advance on, the engine resolves their keys in the new version. Historical assumption sets keep pinning the retired predecessor, which never changes again.

**Sources:** [VersionWorkflow.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/VersionWorkflow.cs), [DimensionVersionStore.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/DimensionVersionStore.cs), [ClosureBuilder.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Dimensions/ClosureBuilder.cs), [DimensionConsumersResolveRule.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Dimensions/DimensionConsumersResolveRule.cs), [DimensionConsumers.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Dimensions/DimensionConsumers.cs), [PredecessorRetirer.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/PredecessorRetirer.cs), [ActiveSetPinner.cs](../../src/Quantalytix.QueryEngine.Admin.Application/AssumptionSets/ActiveSetPinner.cs), [SemanticRevision.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/SemanticRevision.cs), [DimensionsFeatureServiceCollectionExtensions.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Configuration/DimensionsFeatureServiceCollectionExtensions.cs), [ClosureBuilderTests.cs](../../tests/Quantalytix.QueryEngine.Admin.Tests/Dimensions/ClosureBuilderTests.cs), [DimensionConsumersTests.cs](../../tests/Quantalytix.QueryEngine.Admin.Tests/Dimensions/DimensionConsumersTests.cs), [VersionWorkflowTests.cs](../../tests/Quantalytix.QueryEngine.Admin.Tests/Versioning/VersionWorkflowTests.cs).

## Publishing a bridge version

The drawing shows a `bridge_version` validated and then published. All six of its rules read the version's mappings through `BridgeMappingSetReader`, which resolves their member keys in the published versions of the bridge's two dimensions; publishing retires the bridge's previous version, repoints the active assumption set and advances the semantic revision.

```mermaid
sequenceDiagram
    autonumber
    participant Ctl as VersionStatusController
    participant Wf as VersionWorkflow
    participant Store as BridgeVersionStore
    participant Rules as bridge_version rules
    participant Reader as BridgeMappingSetReader
    participant Dimensions as PublishedDimensionReader
    participant Retirer as PredecessorRetirer
    participant Pinner as ActiveSetPinner
    participant Rev as SemanticRevision
    Note over Ctl,Rev: First request, status validated
    Ctl->>Wf: TransitionAsync(bridge_version, id, validated)
    Wf->>Store: GetAsync(id)
    loop The 6 bridge_version rules in registration order
        Wf->>Rules: EvaluateAsync(id)
        Rules->>Reader: ReadAsync(id)
        Reader->>Dimensions: ReadAsync(source dimension), ReadAsync(target dimension)
    end
    Wf->>Store: SetStatusAsync(id, validated)
    Note over Ctl,Rev: Second request, status published
    Ctl->>Wf: TransitionAsync(bridge_version, id, published)
    Wf->>Store: GetAsync(id)
    Wf->>Store: SetStatusAsync(id, published, publishedAt)
    Wf->>Retirer: OnPublishedAsync(id)
    Wf->>Pinner: OnPublishedAsync(id)
    Wf->>Rev: AdvanceAsync()
```

Bridge bindings name the bridge itself, so they need no change to follow the new version: the engine reads the version their assumption set pins.

**Sources:** [VersionWorkflow.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/VersionWorkflow.cs), [BridgeVersionStore.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/BridgeVersionStore.cs), [BridgeMappingSetReader.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Bridges/BridgeMappingSetReader.cs), [PublishedDimensionReader.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Dimensions/PublishedDimensionReader.cs), [PredecessorRetirer.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/PredecessorRetirer.cs), [ActiveSetPinner.cs](../../src/Quantalytix.QueryEngine.Admin.Application/AssumptionSets/ActiveSetPinner.cs), [MappingsAndBridgesFeatureServiceCollectionExtensions.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Configuration/MappingsAndBridgesFeatureServiceCollectionExtensions.cs), [bridge rules](../../src/Quantalytix.QueryEngine.Admin.Application/Bridges), [BridgeMappingRuleTests.cs](../../tests/Quantalytix.QueryEngine.Admin.Tests/Bridges/BridgeMappingRuleTests.cs).

## Activating a release

The drawing shows `POST .../releases/{releaseId}/activate`, the only way a dataset version reaches queries. The pointer is written before the revision advances, so the engine never caches the previous release under the new revision.

```mermaid
sequenceDiagram
    autonumber
    actor Client as Web SPA
    participant Ctl as ReleasesController
    participant Act as ReleaseActivator
    participant Releases as SemanticReleaseRecord gateway
    participant Info as DatabaseInfoRecord gateway
    participant Rev as SemanticRevision
    participant Engine as Query engine
    Client->>Ctl: POST releases/{releaseId}/activate
    Ctl->>Act: ActivateAsync(releaseId)
    Act->>Releases: GetAsync(ReleaseId)
    Act->>Info: GetAsync(SingletonId 1)
    alt The release is not published
        Act-->>Ctl: Activated false, nothing written
        Ctl-->>Client: 409 Release is not published
    else Published
        Act->>Info: UpdateAsync with ActiveReleaseId and UpdatedAt
        Act->>Rev: AdvanceAsync()
        Rev->>Info: GetAsync, then UpdateAsync with SemanticRevision plus one and UpdatedAt
        Act->>Info: GetAsync(SingletonId 1)
        Act-->>Ctl: Activated true with the database info
        Ctl-->>Client: 200 DatabaseInfoRecord
        Note over Engine,Info: On its next request the engine reads the active release and the new revision
    end
```

The release's status is checked before anything is written, so a refused activation leaves `qe_database_info` as it was. Publishing a release does not activate it, and the active release cannot be retired.

**Sources:** [ReleasesController.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Releases/ReleasesController.cs), [ReleaseActivator.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Releases/ReleaseActivator.cs), [ReleaseActivationResult.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Releases/Models/ReleaseActivationResult.cs), [SemanticRevision.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/SemanticRevision.cs), [DatabaseInfoRecord.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Metadata/Models/DatabaseInfoRecord.cs), [ReleaseNotActiveRule.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Releases/ReleaseNotActiveRule.cs), [ReleaseActivationTests.cs](../../tests/Quantalytix.QueryEngine.Admin.Tests/Releases/ReleaseActivationTests.cs); in the query engine checkout: [MetadataQueries.cs](../../../../Quantalytix.QueryEngine/src/Quantalytix.QueryEngine.Metadata.StarRocks/Readers/MetadataQueries.cs).

## Capturing an assumption set

The drawing shows `POST .../assumption-sets`, which captures a historical set: a copy of the active set's pins under a key of its own, with the active release. A request that names the key reads the versions the active set pinned at that moment over that release's dataset versions, so an author can backtest against them later.

```mermaid
sequenceDiagram
    autonumber
    actor Client as Web SPA
    participant Filter as ValidationFilter
    participant Ctl as AssumptionSetsController
    participant Svc as AssumptionSetService
    participant Pins as AssumptionPins
    participant Sets as AssumptionSetRecord gateway
    participant Info as DatabaseInfoRecord gateway
    participant Rev as SemanticRevision
    Client->>Filter: POST assumption-sets with key, display name and description
    Filter->>Filter: AssumptionCaptureInputValidator, a well-formed key no set has
    Filter->>Ctl: valid
    Ctl->>Svc: CaptureAsync(input)
    Svc->>Pins: ActiveAsync()
    Svc->>Info: GetAsync(singleton)
    Svc->>Sets: InsertAsync(historical set with release_id the active release and captured_at now)
    Svc->>Pins: ListAsync(active set)
    Note over Pins: The dimension, mapping, bridge, model and report pins
    Svc->>Pins: InsertAsync(new set, the active pins)
    Svc->>Rev: AdvanceAsync()
    Ctl-->>Client: 201 the historical set
```

Publishing repoints only the active set, so the captured set keeps its versions, which publishing retires and freezes. Activating another release leaves the captured set on its release, which the engine keeps reading after the release is retired. Deleting a historical set removes it with its pins and advances the revision too; the active set cannot be deleted.

**Sources:** [AssumptionSetsController.cs](../../src/Quantalytix.QueryEngine.Admin.Api/AssumptionSets/AssumptionSetsController.cs), [AssumptionSetService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/AssumptionSets/AssumptionSetService.cs), [AssumptionPins.cs](../../src/Quantalytix.QueryEngine.Admin.Application/AssumptionSets/AssumptionPins.cs), [AssumptionCaptureInputValidator.cs](../../src/Quantalytix.QueryEngine.Admin.Application/AssumptionSets/AssumptionCaptureInputValidator.cs), [AssumptionSetTests.cs](../../tests/Quantalytix.QueryEngine.Admin.Tests/AssumptionSets/AssumptionSetTests.cs).

## Verifying and creating a system tenant

The drawing shows a SystemAdmin testing a connection string with `POST api/system/tenants/verify`, then registering the tenant with `POST api/system/tenants`. The same check runs both times: as the answer of `verify`, and as a validation error on `connectionString` when a create would store a string that does not route to the tenant.

```mermaid
sequenceDiagram
    autonumber
    actor Admin as SystemAdmin in the web SPA
    participant Filter as ValidationFilter
    participant Ctl as SystemTenantsController
    participant Verify as TenantConnectionVerification
    participant Verifier as TenantDatabaseVerifier
    participant Factory as StarRocksConnectionFactory
    participant Candidate as Candidate tenant database
    participant Svc as SystemTenantService
    participant Directory as TenantDirectoryRecord gateway
    participant Masker as ConnectionStringMasker
    Admin->>Filter: POST api/system/tenants/verify with tenantId and connectionString
    Note over Admin,Filter: SystemAdmin policy, then TenantVerifyInputValidator checks presence and length
    Filter->>Ctl: Verify(input)
    Ctl->>Verify: CheckAsync(tenantId, connectionString)
    Verify->>Verifier: ProbeAsync(connectionString)
    Verifier->>Factory: OpenAsync(connectionString)
    Factory->>Candidate: open with the API's connection settings
    Verifier->>Candidate: SELECT from qe_database_info WHERE singleton_id = 1
    Candidate-->>Verifier: the row, or an error caught as the failure reason
    Verifier-->>Verify: TenantDatabaseProbe
    Note over Verify: Valid when TenantKey equals tenantId and MetadataSchemaVersion is 4
    Verify-->>Ctl: TenantConnectionCheck with IsValid and Message
    Ctl-->>Admin: 200 TenantConnectionCheck
    Admin->>Filter: POST api/system/tenants with TenantCreateInput
    Filter->>Directory: ListAsync(TenantId), the ID must be unused
    Filter->>Verify: CheckAsync through MustRouteToTenant
    alt The check fails
        Filter-->>Admin: 400 with the reason under connectionString
    else Valid
        Filter->>Ctl: Create(input)
        Ctl->>Svc: CreateAsync(input)
        Svc->>Directory: InsertAsync(row with UpdatedAt)
        Svc->>Masker: Mask(connectionString)
        Masker-->>Svc: the string with its password masked
        Ctl-->>Admin: 201 TenantDirectoryEntry
    end
```

`TenantDirectoryRecord` lives in `query_control`, so its gateway opens the control plane rather than a tenant database. Every response masks the password, so a stored connection string never leaves the server; a later update with no connection string keeps the stored one.

**Sources:** [SystemTenantsController.cs](../../src/Quantalytix.QueryEngine.Admin.Api/SystemTenants/SystemTenantsController.cs), [TenantConnectionVerification.cs](../../src/Quantalytix.QueryEngine.Admin.Application/SystemTenants/TenantConnectionVerification.cs), [TenantDatabaseVerifier.cs](../../src/Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks/TenantVerification/TenantDatabaseVerifier.cs), [StarRocksConnectionFactory.cs](../../src/Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks/Connections/StarRocksConnectionFactory.cs), [TenantVerifyInputValidator.cs](../../src/Quantalytix.QueryEngine.Admin.Application/SystemTenants/TenantVerifyInputValidator.cs), [TenantCreateInputValidator.cs](../../src/Quantalytix.QueryEngine.Admin.Application/SystemTenants/TenantCreateInputValidator.cs), [TenantConnectionRuleExtensions.cs](../../src/Quantalytix.QueryEngine.Admin.Application/SystemTenants/TenantConnectionRuleExtensions.cs), [SystemTenantService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/SystemTenants/SystemTenantService.cs), [ConnectionStringMasker.cs](../../src/Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks/TenantVerification/ConnectionStringMasker.cs), [TableCatalog.cs](../../src/Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks/Tables/TableCatalog.cs).

## A blocked delete answered with 409

The drawing shows `DELETE .../datasets/{datasetId}`. The service collects every reason the delete is blocked before it deletes anything; only when nothing blocks it does it delete, children first.

```mermaid
sequenceDiagram
    autonumber
    actor Client as Web SPA
    participant Ctl as DatasetsController
    participant Svc as DatasetService
    participant Datasets as DatasetRecord gateway
    participant Versions as DatasetVersionRecord gateway
    participant Manifest as ReleaseDatasetRecord gateway
    participant Releases as SemanticReleaseRecord gateway
    participant Blockers as DeleteBlockers
    participant VersionSvc as DatasetVersionService
    Client->>Ctl: DELETE datasets/{datasetId}
    Ctl->>Svc: DeleteAsync(datasetId)
    Svc->>Datasets: GetAsync(DatasetId)
    Svc->>Versions: ListAsync(DatasetId)
    Svc->>Manifest: ListAsync(DatasetId)
    Svc->>Releases: ListAsync(ReleaseId in the listing releases)
    Svc->>Blockers: new DeleteBlockers(Dataset and its key)
    Svc->>Blockers: AddNamed(it has non-draft versions)
    Svc->>Blockers: AddNamed(listed by releases)
    Svc->>Blockers: Detail()
    alt A reason was added
        Blockers-->>Svc: one sentence naming every reason
        Svc-->>Ctl: detail
        Ctl-->>Client: 409 Problem titled DeleteBlockedProblem.Title, Delete blocked
    else Nothing blocks the delete
        Blockers-->>Svc: null
        Svc->>VersionSvc: DeleteAsync(every version ID)
        VersionSvc->>VersionSvc: DatasetVersionContentStore.DeleteAsync, children first, then the versions
        Svc->>Datasets: DeleteAsync(DatasetId)
        Ctl-->>Client: 204
    end
```

`DeleteBlockers` lists at most ten names per reason and counts the rest. The same pattern guards the other 19 deletes that answer "Delete blocked", with the checks of each slice, such as `FieldUsage` for fields, `DimensionDeleteChecks` for dimensions, levels and members, and `ReportUses` for models and the model content live reports name. Deleting a draft version of a living kind has no blockers, because other objects name the identity or keys of its published version, never a draft.

**Sources:** [DatasetsController.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Datasets/DatasetsController.cs), [DatasetService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Datasets/DatasetService.cs), [DeleteBlockers.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Metadata/DeleteBlockers.cs), [DeleteBlockedProblem.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Errors/DeleteBlockedProblem.cs), [DatasetVersionService.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Datasets/DatasetVersionService.cs), [DatasetVersionContentStore.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Datasets/DatasetVersionContentStore.cs), [FieldUsage.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Datasets/FieldUsage.cs), [DimensionDeleteChecks.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Dimensions/DimensionDeleteChecks.cs), [ReportUses.cs](../../src/Quantalytix.QueryEngine.Admin.Application/CalculationModels/ReportUses.cs).
