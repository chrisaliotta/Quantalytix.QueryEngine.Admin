import { CheckIcon, ChevronDownIcon, MoreHorizontalIcon, Trash2Icon } from 'lucide-react'
import { DetailHeader, DetailKey, DetailTitle } from '@/components/shell/page-shell'
import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { useVersionLabel, VersionBadge } from '@/components/snapshots/shared'
import { useDimensionsApi } from '@/lib/dimensions/client'
import { POLICY_TEXT, type DimensionDetail, type DimensionVersionSummary } from '@/lib/dimensions/contract'
import { cn } from '@/lib/utils'
import { useHasPendingChanges } from '@/components/shell/changes'
import { DeleteDialog, KindBadge, SystemBadge, formatDate, useDimCall } from './shared'

interface Props {
  dim: DimensionDetail
  selected: DimensionVersionSummary
  onBack: () => void
  onSelectVersion: (id: string) => void
}

export function DimensionHeader({ dim, selected, onBack, onSelectVersion }: Props) {
  const api = useDimensionsApi()
  const { run } = useDimCall()
  const dirty = useHasPendingChanges()
  const versionLabel = useVersionLabel()
  const [deleting, setDeleting] = useState(false)
  const system = dim.origin === 'system'

  return (
    <>
      <DetailHeader
        section="Dimensions"
        onBack={onBack}
        title={<DetailTitle>{dim.displayName}</DetailTitle>}
        meta={
          <>
            <DetailKey>{dim.key}</DetailKey>
            <KindBadge kind={dim.kind} />
            {dim.profile && (
              <span title={`Required by ${dim.profile.owner} · ${dim.profile.editPolicy}: ${POLICY_TEXT[dim.profile.editPolicy](dim.profile.owner)}`}>
                <SystemBadge name={dim.profile.displayName} />
              </span>
            )}
            {!dim.isEnabled && (
              <Badge variant="outline" className="font-normal text-muted-foreground">
                Disabled
              </Badge>
            )}
          </>
        }
      >
        <DropdownMenu>
          <DropdownMenuTrigger render={<Button variant="outline" size="sm" aria-label={`Version ${versionLabel(selected)}`} />}>
            <VersionBadge version={selected} className="border-0 bg-transparent px-0" />
            <ChevronDownIcon className="opacity-60" />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-72">
            <DropdownMenuLabel className="text-xs font-normal text-muted-foreground">Versions</DropdownMenuLabel>
            {dim.versions.map((v) => (
              <DropdownMenuItem key={v.id} onClick={() => onSelectVersion(v.id)} className="items-start gap-2">
                <CheckIcon className={cn('mt-0.5', v.id === selected.id ? 'opacity-100' : 'opacity-0')} />
                <span className="flex min-w-0 flex-1 flex-col">
                  <span className="flex items-center gap-2">
                    <VersionBadge version={v} />
                    <span className="ml-auto text-xs tabular-nums text-muted-foreground">{v.memberCount} members</span>
                  </span>
                  <span className="truncate text-xs text-muted-foreground">{v.isLive ? 'Edited in place' : `Captured ${formatDate(v.createdAt)}`}</span>
                </span>
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
        {!selected.isLive && (
          <Button variant="ghost" size="sm" onClick={() => onSelectVersion(dim.liveVersionId)}>
            Go to live
          </Button>
        )}
        {!system && (
          <DropdownMenu>
            <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label="Dimension actions" />}>
              <MoreHorizontalIcon />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-52">
              <DropdownMenuItem variant="destructive" disabled={dirty} onClick={() => setDeleting(true)}>
                <Trash2Icon /> Delete dimension…
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        )}
      </DetailHeader>

      {!system && (
        <DeleteDialog
          target={deleting ? { type: 'dimension', dimensionId: dim.id } : null}
          title={`Delete ${dim.displayName}?`}
          onOpenChange={setDeleting}
          onConfirm={async () => {
            const r = await run(() => api.deleteDimension(dim.id), `Deleted ${dim.displayName}`)
            if (r.ok) onBack()
            return r.ok
          }}
        />
      )}
    </>
  )
}
