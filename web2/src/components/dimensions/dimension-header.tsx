import { CheckIcon, ChevronDownIcon, MoreHorizontalIcon, SnowflakeIcon, Trash2Icon } from 'lucide-react'
import { DetailHeader, DetailKey, DetailTitle } from '@/components/shell/page-shell'
import { useState } from 'react'
import { toast } from 'sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { useDimensionsApi, useRefreshDimensions } from '@/lib/dimensions/client'
import { POLICY_TEXT, versionLabel, type DimensionDetail, type ValidationReport, type VersionSummary } from '@/lib/dimensions/contract'
import { cn } from '@/lib/utils'
import { SAVE_FIRST, useHasPendingChanges } from '@/components/shell/changes'
import { ConfirmDialog, DeleteDialog, KindBadge, STATUS_TONE, SystemBadge, formatDate, useDimCall } from './shared'

interface Props {
  dim: DimensionDetail
  selected: VersionSummary
  onBack: () => void
  onSelectVersion: (id: string) => void
  onReport: (r: ValidationReport) => void
}

export function DimensionHeader({ dim, selected, onBack, onSelectVersion, onReport }: Props) {
  const api = useDimensionsApi()
  const refresh = useRefreshDimensions()
  const { run } = useDimCall()
  const dirty = useHasPendingChanges()
  const [deleting, setDeleting] = useState(false)
  const [deletingVersion, setDeletingVersion] = useState(false)
  const [freezing, setFreezing] = useState(false)
  const [note, setNote] = useState('')
  const system = dim.origin === 'system'
  const locked = dim.profile?.editPolicy === 'locked'
  const versions = [...dim.versions].sort((a, b) => b.versionNumber - a.versionNumber)
  const live = versions.find((v) => v.status === 'published')
  const isLive = selected.status === 'published'
  const canDeleteVersion = !locked && !isLive

  async function freeze() {
    const r = await api.freezeVersion(selected.id, note)
    setFreezing(false)
    if (!r.ok) return toast.error(r.problem.title, { description: r.problem.detail })
    if (!r.data.live) {
      onReport(r.data.report)
      toast.error(`v${selected.versionNumber} can't be frozen yet`, { description: 'Fix the violations listed above.' })
      return
    }
    setNote('')
    await refresh()
    onSelectVersion(r.data.live.id)
    toast.success(`Froze v${selected.versionNumber}`, { description: `Editing continues in v${r.data.live.versionNumber}.` })
  }

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
        {!isLive && (
          <span className="hidden text-xs text-muted-foreground lg:inline">
            Frozen {formatDate(selected.retiredAt)}
            {selected.freezeNote ? ` · ${selected.freezeNote}` : ''}
          </span>
        )}
        <DropdownMenu>
          <DropdownMenuTrigger render={<Button variant="outline" size="sm" aria-label={`Version v${selected.versionNumber}, ${versionLabel(selected.status)}`} />}>
            <span className={cn('size-1.5 rounded-full', isLive ? 'bg-success' : 'bg-muted-foreground')} aria-hidden />
            <span className="font-mono">v{selected.versionNumber}</span>
            <span className="text-muted-foreground">{versionLabel(selected.status)}</span>
            <ChevronDownIcon className="opacity-60" />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-72">
            <DropdownMenuLabel className="text-xs font-normal text-muted-foreground">Versions</DropdownMenuLabel>
            {versions.map((v) => (
              <DropdownMenuItem key={v.id} onClick={() => onSelectVersion(v.id)} className="items-start gap-2">
                <CheckIcon className={cn('mt-0.5', v.id === selected.id ? 'opacity-100' : 'opacity-0')} />
                <span className="flex min-w-0 flex-1 flex-col">
                  <span className="flex items-center gap-2">
                    <span className="font-mono text-sm">v{v.versionNumber}</span>
                    <span className={cn('rounded-full border px-1.5 text-[11px] leading-4', STATUS_TONE[v.status])}>{versionLabel(v.status)}</span>
                    <span className="ml-auto text-xs tabular-nums text-muted-foreground">{v.memberCount} members</span>
                  </span>
                  <span className="truncate text-xs text-muted-foreground">
                    {v.status === 'published' ? `Live since ${formatDate(v.publishedAt)}` : `Frozen ${formatDate(v.retiredAt)}${v.freezeNote ? ` · ${v.freezeNote}` : ''}`}
                  </span>
                </span>
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
        {isLive && !locked && (
          <Button variant="outline" size="sm" disabled={dirty} title={dirty ? SAVE_FIRST : undefined} onClick={() => setFreezing(true)}>
            <SnowflakeIcon /> Freeze
          </Button>
        )}
        {!isLive && live && (
          <Button variant="ghost" size="sm" onClick={() => onSelectVersion(live.id)}>
            Go to live
          </Button>
        )}
        {(!system || canDeleteVersion) && (
          <DropdownMenu>
            <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label="Dimension actions" />}>
              <MoreHorizontalIcon />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-52">
              {canDeleteVersion && (
                <DropdownMenuItem variant="destructive" disabled={dirty} onClick={() => setDeletingVersion(true)}>
                  <Trash2Icon /> Delete frozen v{selected.versionNumber}…
                </DropdownMenuItem>
              )}
              {canDeleteVersion && !system && <DropdownMenuSeparator />}
              {!system && (
                <DropdownMenuItem variant="destructive" disabled={dirty} onClick={() => setDeleting(true)}>
                  <Trash2Icon /> Delete dimension…
                </DropdownMenuItem>
              )}
            </DropdownMenuContent>
          </DropdownMenu>
        )}
      </DetailHeader>

      <ConfirmDialog open={freezing} onOpenChange={setFreezing} title={`Freeze v${selected.versionNumber}?`} confirmLabel="Freeze" onConfirm={freeze}>
        <span className="flex flex-col gap-3">
          <span>
            v{selected.versionNumber} becomes a read-only snapshot that reports and assumption sets can pin. Editing continues in v{selected.versionNumber + 1}, which starts as an exact copy and goes live
            immediately.
          </span>
          <Input aria-label="Freeze note" placeholder="Note, e.g. FY26 close (optional)" value={note} onChange={(e) => setNote(e.target.value)} />
        </span>
      </ConfirmDialog>
      <DeleteDialog
        target={deletingVersion ? { type: 'version', versionId: selected.id } : null}
        title={`Delete frozen v${selected.versionNumber}?`}
        description="Its members, levels and bands are removed with it."
        onOpenChange={setDeletingVersion}
        onConfirm={async () => {
          const fallback = live ?? versions.find((v) => v.id !== selected.id)
          const r = await run(() => api.deleteVersion(selected.id), `Deleted v${selected.versionNumber}`)
          if (r.ok && fallback) onSelectVersion(fallback.id)
          return r.ok
        }}
      />
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
