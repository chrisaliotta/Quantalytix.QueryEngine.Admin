# Query Engine Admin API architecture diagrams

The Query Engine Admin API is the .NET 10 ASP.NET Core Web API that authors the semantic metadata the Quantalytix query engine reads from StarRocks: datasets with their fields, grains, bindings and measures; dimensions with their levels, members and range bands; mapping sets, bridges, semantic releases and assumption sets; calculation models with their variables and formulas, and the reports over them with their rows, sources and the bridges they own, variables, profiles, calculation dimensions and columns; and the control plane's tenant directory. It writes each tenant's `qe_*` tables through table gateways, one statement per call, moves the five versioned kinds, three of them living, through a draft, validated, published and retired lifecycle guarded by publication and retirement rules, checks every change to an enabled report, and to the metrics of a calculation model, before writing it, and advances the tenant's semantic revision whenever rows the engine may have cached change. The engine never sees a draft or a disabled report: it loads published rows, enabled reports and the models they read through the `vw_qe_*` views, and dataset versions only through the active release.

Describes `main` as of 2026-10-20, after eef70ae. The drawings describe implemented behavior only.

| Page | Drawings |
| --- | --- |
| [High-level architecture](01-architecture.md) | Runtime components, project dependencies, composition root; feature map table |
| [Request pipeline](02-request-pipeline.md) | A tenant-scoped write, development sign-in and session, tenant routing; error responses table |
| [Core classes](03-core-classes.md) | Authentication and tenancy, persistence, versioning, errors and validation |
| [Version lifecycle](04-version-lifecycle.md) | Lifecycle states of the standard kinds and of assumption sets, leaving draft, publishing, retiring, the report gate, the model gate; semantic revision and lifecycle handler tables |
| [Feature classes](05-feature-classes.md) | One class diagram per feature area: datasets, dimensions, mappings, bridges, bindings, measures, releases, assumption sets, system tenants, calculation models and reports, the report editor |
| [Key actions](06-key-actions.md) | Thirteen write sequences, from creating a dataset to a blocked delete, in the order their statements run |

Endpoint-by-endpoint detail, with request and response schemas and status codes, is in the [OpenAPI document](../../openapi/admin-api.json), which every build regenerates. Building, configuring, testing and running the API are in the [API README](../../README.md). The query engine documents its side in its own [architecture diagrams](../../../../Quantalytix.QueryEngine/docs/architecture/README.md); links into the query engine assume its checkout sits beside this repository, at the default `QueryEngineRoot`.

**Sources:** [API README](../../README.md), [OpenAPI document](../../openapi/admin-api.json), [Program.cs](../../src/Quantalytix.QueryEngine.Admin.Api/Program.cs), [Directory.Build.props](../../Directory.Build.props), [VersionKinds.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Vocabulary/VersionKinds.cs), [SemanticRevision.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Versioning/SemanticRevision.cs), [ITableGateway.cs](../../src/Quantalytix.QueryEngine.Admin.Application/Metadata/ITableGateway.cs).

## Reading the diagrams

The diagrams are Mermaid source embedded in Markdown, so they are edited as text and reviewed in diffs. Open them in any Mermaid-capable Markdown viewer, such as GitHub or the Markdown preview of Cursor or VS Code with Mermaid support. No database connection or credentials are needed to read them.

- **Class diagrams:** a dotted line with a triangle means interface implementation, a solid line with a triangle means inheritance, a solid arrow an association (a constructor dependency or a field), and a dotted arrow a use (a call, a creation or a return). Members show the operations that matter; cancellation tokens, overloads and most properties are omitted, and generic types are written with tildes, so `ITableGateway~TRecord~` is `ITableGateway<TRecord>`.
- **Flowcharts:** arrows are calls or data flow, except in the project dependencies drawing, where they are project references.
- **Sequence diagrams:** they summarize async calls without showing every await, and show a gateway as the record it serves, such as "DatasetRecord gateway". Validation and the draft check appear only where they matter to the action.
- **Sources:** every section ends with links to the files used to verify it. Links into `../../src` are relative to this folder; links marked as in the query engine checkout point into the sibling repository.

**Sources:** [01-architecture.md](01-architecture.md), [02-request-pipeline.md](02-request-pipeline.md), [03-core-classes.md](03-core-classes.md), [04-version-lifecycle.md](04-version-lifecycle.md), [05-feature-classes.md](05-feature-classes.md), [06-key-actions.md](06-key-actions.md), [engine architecture README](../../../../Quantalytix.QueryEngine/docs/architecture/README.md).
