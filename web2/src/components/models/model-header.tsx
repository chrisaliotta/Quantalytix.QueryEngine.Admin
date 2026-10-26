import { MoreHorizontalIcon, Trash2Icon } from 'lucide-react'
import { DetailHeader, DetailKey } from '@/components/shell/page-shell'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { EditableLabel } from '@/components/report/shared/editable-label'
import { useModels } from '@/lib/model/store'
import type { CalcModel } from '@/lib/model/types'

interface ModelHeaderProps {
  model: CalcModel
  saved: CalcModel
  onBack: () => void
  onDelete: () => void
}

/** Save/Discard/⌘S come from the ChangeScope provided by ModelDetail, rendered by DetailHeader. */
export function ModelHeader({ model, saved, onBack, onDelete }: ModelHeaderProps) {
  const { edit } = useModels()
  const enabledChanged = model.enabled !== saved.enabled

  return (
    <DetailHeader
      section="Calculation models"
      onBack={onBack}
      title={
        <EditableLabel
          aria-label="Model name"
          value={model.name}
          onCommit={(v) => {
            if (v) edit(model.key, (d) => void (d.name = v))
          }}
          showIcon
          className="text-sm font-semibold"
        />
      }
      meta={
        <>
          <DetailKey>{model.key}</DetailKey>
          <div className="ml-2 flex items-center gap-2">
            <Switch id="model-enabled" checked={model.enabled} onCheckedChange={(next) => edit(model.key, (d) => void (d.enabled = next))} />
            <Label htmlFor="model-enabled" className="text-xs text-muted-foreground">
              {model.enabled ? 'Enabled' : 'Disabled'}
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
