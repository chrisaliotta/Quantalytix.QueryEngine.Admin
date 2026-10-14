## Monaco editor theming with host CSS variables

This component integrates Monaco Editor with the host application's theming system, which
exposes colors as CSS variables, including modern color spaces like `oklch(...)`.

Monaco's theme engine does not understand modern CSS color spaces and is strict about formats.
In practice, we treat it as accepting only solid sRGB hex colors like `#1e1e1e` and `#fafafa`.

However, a host app may define colors using `oklch`, for example:

```css
html.theme-some-id {
  --background: oklch(0.2355 0.0119 270.87);
  --foreground: oklch(0.985 0 0);
  --primary: oklch(0.922 0 0);
  --muted-foreground: oklch(0.708 0 0);
}
```

If we pass those values directly into Monaco's theme API, it can throw runtime errors like:

```text
Error: Illegal value for token color: oklch(0.985 0 0)
```

To bridge this gap, `normalizeColorForMonaco()` uses a tiny canvas so the browser resolves any
valid CSS color into a plain hex string that Monaco accepts.

When editing this component:

- Always read colors from CSS variables via `getCssVarValue(...)`.
- Always normalize anything that might not already be hex before passing it to Monaco.
- If you add new Monaco theme colors, follow the existing normalization pattern.
