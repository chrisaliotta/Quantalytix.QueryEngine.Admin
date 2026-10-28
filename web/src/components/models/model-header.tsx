import { MoreHorizontalIcon, Trash2Icon } from 'lucide-react'
import { DetailHeader, DetailKey } from '@/components/shell/page-shell'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { EditableLabel } from '@/components/report/shared/editable-label'
import type { CalcModelPatch, ModelDocument, Violation } from '@/lib/models/contract'
import { useModelEdit } from './use-model-edit'

interface ModelHeaderProps {
  model: ModelDocument
  /** Whether the saved model is enabled, so a pending change shows that it takes effect on save. */
  savedEnabled: boolean
  onBack: () => void
  onDelete: () => void
  onRefused: (violations: Violation[] | null) => void
}

/** Save/Discard/⌘S come from the ChangeScope provided by ModelDetail, rendered by DetailHeader. */
export function ModelHeader({ model, savedEnabled, onBack, onDelete, onRefused }: ModelHeaderProps) {
  const run = useModelEdit()
  const enabledChanged = model.isEnabled !== savedEnabled
  const update = async (patch: CalcModelPatch) => onRefused(await run((api) => api.updateModel(model.id, patch)))

  return (
    <DetailHeader
      section="Calculation models"
      onBack={onBack}
      title={
        <EditableLabel
          aria-label="Model name"
          value={model.displayName}
          onCommit={(v) => {
            if (v) void update({ displayName: v })
          }}
          showIcon
          className="text-sm font-semibold"
        />
      }
      meta={
        <>
          <DetailKey>{model.key}</DetailKey>
          <div className="ml-2 flex items-center gap-2">
            <Switch id="model-enabled" checked={model.isEnabled} onCheckedChange={(isEnabled) => void update({ isEnabled })} />
            <Label htmlFor="model-enabled" className="text-xs text-muted-foreground">
              {model.isEnabled ? 'Enabled' : 'Disabled'}
              {enabledChanged && <span className="sr-only"> (takes effect when you save)</span>}
              {enabledChanged && <span aria-hidden className="size-1.5 rounded-full bg-warning" title="Takes effect when you save" />}
            </Label>
          </div>
        </>
      }
    >
      <DropdownMenu>
        <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label="Model actions" />}>
          <MoreHorizontalIcon />
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-56">
          <DropdownMenuItem variant="destructive" onClick={onDelete}>
            <Trash2Icon />
            Delete model…
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </DetailHeader>
  )
}
