# Error Page

Full-screen error view for HTTP status codes, built from **Base** with preset variants for common status codes. Uses shadcn `Button` for actions.

## Structure

| Folder   | Component      | Default title                                |
| -------- | -------------- | -------------------------------------------- |
| **Base** | `ErrorPage`    | Configurable (default 404, "Page Not Found") |
| **400**  | `ErrorPage400` | Bad Request                                  |
| **401**  | `ErrorPage401` | Unauthorized                                 |
| **402**  | `ErrorPage402` | Payment Required                             |
| **403**  | `ErrorPage403` | Access Denied                                |
| **404**  | `ErrorPage404` | Page Not Found                               |
| **405**  | `ErrorPage405` | Method Not Allowed                           |
| **408**  | `ErrorPage408` | Request Timeout                              |
| **409**  | `ErrorPage409` | Conflict                                     |
| **410**  | `ErrorPage410` | Gone                                         |
| **429**  | `ErrorPage429` | Too Many Requests                            |
| **500**  | `ErrorPage500` | Internal Server Error                        |
| **502**  | `ErrorPage502` | Bad Gateway                                  |
| **503**  | `ErrorPage503` | Service Unavailable                          |
| **504**  | `ErrorPage504` | Gateway Timeout                              |

Each variant is a thin wrapper over Base with fixed `statusCode` and default title/description; you can override `title`, `description`, and pass through `actions`, `onGoBack`, `onReturnHome`, `className`.

## Types

Re-exported from Base:

- **`ErrorPageProps`** — props for `<ErrorPage />`
- **`ErrorPageAction`** — item in the `actions` array
- **`ErrorPageButtonVariant`** — shadcn Button variant union

Variant props (optional `title`/`description`, rest from Base):  
`ErrorPage400Props`, `ErrorPage401Props`, `ErrorPage402Props`, `ErrorPage403Props`, `ErrorPage404Props`, `ErrorPage405Props`, `ErrorPage408Props`, `ErrorPage409Props`, `ErrorPage410Props`, `ErrorPage429Props`, `ErrorPage500Props`, `ErrorPage502Props`, `ErrorPage503Props`, `ErrorPage504Props`.

```ts
import {
  ErrorPage,
  ErrorPage404,
  type ErrorPage404Props,
  ErrorPage503,
  type ErrorPage503Props,
  type ErrorPageProps,
} from '@registry/pages/ErrorPage';
```

## Usage

**Base – any status:**

```tsx
<ErrorPage
  statusCode={418}
  title="I'm a teapot"
  description="This server refuses to brew coffee."
  onReturnHome={() => navigate('/')}
/>
```

**Variants (default copy):**

```tsx
<ErrorPage404 onGoBack={() => history.back()} onReturnHome={() => navigate('/')} />
<ErrorPage403 onReturnHome={() => navigate('/')} />
<ErrorPage500 onReturnHome={() => navigate('/')} />
<ErrorPage503 title="Maintenance" description="Back in 5 minutes." onReturnHome={() => navigate('/')} />
```

## Props (Base)

| Prop           | Type                | Default            | Description                                                |
| -------------- | ------------------- | ------------------ | ---------------------------------------------------------- |
| `statusCode`   | `number`            | `404`              | HTTP status code shown.                                    |
| `title`        | `ReactNode`         | `"Page Not Found"` | Main heading.                                              |
| `description`  | `ReactNode`         | (see Base)         | Short description.                                         |
| `actions`      | `ErrorPageAction[]` | —                  | Custom buttons (overrides onGoBack/onReturnHome when set). |
| `onGoBack`     | `() => void`        | —                  | "Go Back" callback.                                        |
| `onReturnHome` | `() => void`        | —                  | "Return to Home" callback.                                 |
| `className`    | `string`            | —                  | Root container class.                                      |

## Styling (data-slot)

Same as Base: `error-page`, `error-page-content`, `error-page-status`, `error-page-title`, `error-page-description`, `error-page-actions`, `error-page-action`. See `Base/BasePage.css` and theme variables.

## Files

- `index.tsx` — Re-exports Base + all variants and their types.
- `Base/` — `ErrorPage` (BasePage.tsx), `types.ts`, `variants.ts`, `BasePage.css`, `index.tsx`.
- `400/` … `504/` — One folder per variant; each `index.tsx` exports `ErrorPage{N}` and `ErrorPage{N}Props` (default copy from `Base/variants.ts`).
- `ErrorPage.css` — Imports Base styles (satisfies registry).
- `ErrorPage.custom.css` — Example custom styles for data-slot overrides (e.g. Storybook).
- `ErrorPage.stories.tsx` — Storybook (Base + all variants).
- `README.md` — This file.
