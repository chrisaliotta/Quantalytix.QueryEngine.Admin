# Query Engine Admin API architecture

These pages describe the Admin API as it is implemented on branch `feature/web2-refactor`, after the refactor to the
contract-first design of the [blueprint](../../../docs/blueprint/README.md). The blueprint holds the decisions and their
reasons; these pages describe the code that implements them. Endpoint-level detail (paths, schemas, status codes) is in the
[contract](../../openapi/contract/README.md). Building, configuring, testing and running the API are in the
[API README](../../README.md).

| Page | Covers |
|---|---|
| [High-level architecture](01-architecture.md) | Layers and projects, the contract-first rule, the composition root, the feature map, how `web2` consumes the API |
| [Request pipeline](02-request-pipeline.md) | Middleware, authentication and tenancy, the route existence check, input validation, JSON and string ids, the problem shape, OpenAPI generation, vocabularies |
| [Core classes](03-core-classes.md) | The change-set pipeline, snapshot rules and their stages, the write plan, temporary ids, refusals, delete checks, table gateways, the conformance test |
| [Version lifecycle](04-version-lifecycle.md) | Strict and living kinds, the version workflow, publication and retirement rules, freeze, the active assumption set and release, the semantic revision |
| [Feature classes](05-feature-classes.md) | Each feature module's controllers, services, aggregate and rules |
| [Key actions](06-key-actions.md) | Sequences of the main requests: a dimension commit, a dataset commit and publish, a release activation, a report commit, a freeze, a delete check |

The diagrams are Mermaid source embedded in Markdown. In sequence diagrams a gateway appears as the table it writes, and
async calls are shown without every await.
