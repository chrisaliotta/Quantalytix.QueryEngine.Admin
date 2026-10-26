import { useSyncExternalStore } from 'react'

export type ViewerRole = 'system_admin' | 'report_author'

export interface Viewer {
  name: string
  role: ViewerRole
}

export const ROLE_LABEL: Record<ViewerRole, string> = {
  system_admin: 'System admin',
  report_author: 'Report author',
}

/** Prototype session. A real app reads this from its identity provider; the server enforces it. */
let viewer: Viewer = { name: 'Dana Whitfield', role: 'system_admin' }
const listeners = new Set<() => void>()

export function getViewer(): Viewer {
  return viewer
}

export function setViewerRole(role: ViewerRole) {
  viewer = { ...viewer, role }
  listeners.forEach((l) => l())
}

const subscribe = (cb: () => void) => {
  listeners.add(cb)
  return () => listeners.delete(cb)
}

export function useViewer(): Viewer & { isSystemAdmin: boolean } {
  const v = useSyncExternalStore(subscribe, getViewer, getViewer)
  return { ...v, isSystemAdmin: v.role === 'system_admin' }
}
