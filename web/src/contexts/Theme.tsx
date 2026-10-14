import { createContext, use, useEffect, useMemo, useState, type ReactNode } from 'react';

export type Theme = 'dark' | 'light' | string;

interface ThemeContextType {
  theme: Theme;
  setTheme: (theme: Theme) => void;
}

const ThemeContext = createContext<ThemeContextType>({
  theme: 'dark',
  setTheme: () => {},
});

/**
 * Applies the theme class (`dark` | `light`) to `<html>`; mirrors the registry's ThemeProvider.
 * `theme` is the initial theme; change it later with `useTheme().setTheme`.
 */
export function ThemeProvider({
  theme: initialTheme = 'dark',
  children,
}: {
  theme?: Theme;
  children: ReactNode;
}) {
  const [theme, setTheme] = useState<Theme>(initialTheme);

  useEffect(() => {
    document.documentElement.classList.remove('dark', 'light');
    document.documentElement.classList.add(theme);
  }, [theme]);

  const value = useMemo(() => ({ theme, setTheme }), [theme]);

  return <ThemeContext value={value}>{children}</ThemeContext>;
}

/** Current theme and its setter. */
// eslint-disable-next-line react-refresh/only-export-components
export function useTheme() {
  return use(ThemeContext);
}
