# Query Engine Admin web architecture

The Query Engine Admin web app is the browser client of the admin API. It is a React 19 single-page app, built with Vite and TypeScript, that edits one tenant's semantic metadata at a time under `/t/:tenantId/...` and lets SystemAdmins switch tenants and edit the tenant directory. It calls the API through a typed openapi-fetch client, keeps server state in TanStack Query, builds its forms with react-hook-form and leaves every validation rule to the API. It builds to static files for a static host.

Describes `main` as of 2026-10-20, after eef70ae. The drawings describe implemented behavior only.

| Page | Drawings |
| --- | --- |
| [High-level architecture](01-architecture.md) | Runtime topology in development and production; source layers and their imports; generated API types |
| [Composition](02-composition.md) | Provider chain; route tree; a feature's routes and breadcrumbs; app shell layout; anatomy of a feature module; composition of the dataset page |
| [How it works](03-how-it-works.md) | Data layer; `ApiProblem`; pickers; forms; strict and living version lifecycles; `VersionHeader` wiring; code splitting; theming and the registry; shared building blocks |
| [Critical flows](04-critical-flows.md) | Sign-in; session restore; a 401; switching tenants; a list page; a 400 on save; a 422 on a transition; cloning a version; bulk paste; activating a release; editable and read-only versions; building a report |

**Legend.** In flowcharts, arrows mean composition (renders, provides) or calls (imports, calls), as the sentence above each drawing says; edge labels name the call, prop or condition. In the class diagram, a hollow triangle means inheritance and a plain arrow an association. Sequence diagrams summarize async calls: solid arrows are calls, dashed arrows are returns, and they do not show every `await`, re-render or TanStack Query callback. Names in drawings and tables match the exported names in the code; paths are relative to `web/` unless they start with `api/`.

**How to view.** Open the pages in any Mermaid-capable Markdown viewer, such as the Markdown preview of Cursor or VS Code, or GitHub. Nothing needs to be built or running.

**Related documents.** The [web app README](../../README.md) covers setup and scripts. The admin API documents itself in [its architecture pages](../../../api/docs/architecture/README.md); the pages here link to them where the web hands a request to the API.

Each section ends with the files used to verify its drawing.

**Sources:** [README.md](../../README.md), [package.json](../../package.json), [router.tsx](../../src/app/router.tsx), [App.tsx](../../src/app/App.tsx).
