type CssVarOptions = {
  fallback?: string;
  element?: HTMLElement | null;
};

export function getCssVarValue(name: string, options: CssVarOptions = {}): string {
  const { fallback = '', element } = options;

  if (typeof window === 'undefined') return fallback;

  const target = element ?? window.document.documentElement;
  const styles = window.getComputedStyle(target);
  const value = styles.getPropertyValue(name);

  return (value && value.trim()) || fallback;
}
