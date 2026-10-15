# Query Engine Admin web

A React 19 single-page app, built with Vite and TypeScript, for the Query Engine Admin API. It edits one tenant's
semantic metadata at a time under `/t/:tenantId/...`, and lets system administrators switch tenants and edit the tenant
directory. It builds to static files for a static host.

It uses Tailwind v4, shadcn components (`src/components/ui`) and the in-house `@qtx` registry (`src/registry`), react-router 7,
TanStack Query, react-hook-form, and openapi-fetch typed from the API's OpenAPI document. The API owns every validation
rule: forms show its `ValidationProblemDetails` on their fields rather than checking input themselves.

## Prerequisites

- Node.js and pnpm 11; `package.json` pins `pnpm@11.10.0` through `packageManager`.
- The admin API, running on `http://localhost:5180` (see `../api/README.md`).

## Configuration

`VITE_API_BASE_URL` is the admin API's base URL, without a trailing slash. Vite inlines it at build time, so set it per
environment, for example in `.env.production` or the CI build step, and allow the app's origin in the API's
`Cors:AllowedOrigins`. `.env.example` documents the variable.

In development, `.env.development` leaves it empty: the app calls its own origin, and the dev server proxies `/api` and
`/development` to `http://localhost:5180` (`server.proxy` in `vite.config.ts`). The browser then needs no CORS entry and
no route to the API's port, which matters when it runs outside the machine or WSL instance that runs the servers.

## Develop

```bash
pnpm install
pnpm dev        # http://localhost:5173
```

Sign in at `/login`. A development build asks the API for a development token (`POST /development/token`) for the tenant
you enter, as a SystemAdmin if you tick the box. A production identity provider plugs in at `src/auth/AuthClient.ts`.

## Build, lint and test

```bash
pnpm build      # tsc -b, then vite build into dist/
pnpm lint       # oxlint
pnpm test       # vitest
```

Tests run in the `Asia/Tokyo` time zone (`test.env` in `vite.config.ts`). The metadata stores UTC and the app shows and
edits UTC, so running away from UTC catches any time handled in local time.

## API types

`src/api/schema.d.ts` is generated from `../api/openapi/admin-api.json`, which every API build writes:

```bash
pnpm api:types
```

Commit it with the API change that produced it. Call the API through `apiClient` and `unwrap` from `src/api`, and take
request and response types from `paths` and `components`.

## Layout

- `src/app`: the shell, router and navigation.
- `src/auth`: sign-in.
- `src/api`: the API client and problem details.
- `src/shared`: building blocks such as entity tables, form sheets and fields, the version header, lookups and editors.
- `src/features/<feature>`: the pages of each feature. Each `routes.tsx` lazy-loads them, so every page is its own
  chunk.
- `public/staticwebapp.config.json`: the static host's fallback of unknown paths to `index.html`.
- `pnpm registry:serve` serves a local `@qtx` registry checkout on port 6006, for `shadcn add @qtx/<item>`.
