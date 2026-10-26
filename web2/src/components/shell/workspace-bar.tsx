import { ActiveReleaseChip } from '@/components/releases/workspace-extras'

/** Top bar of a section's list page. Detail pages render their own header instead. */
export function WorkspaceBar({ title }: { title: string }) {
  return (
    <header className="flex h-12 shrink-0 items-center gap-3 border-b bg-card px-4">
      <p className="text-sm font-medium">{title}</p>
      <div className="ml-auto flex items-center gap-2">
        <ActiveReleaseChip />
      </div>
    </header>
  )
}
