import { CheckIcon, ClipboardCheckIcon, CopyIcon, GitCompareIcon, MoreHorizontalIcon, PackageIcon } from 'lucide-react'
import { useState } from 'react'
import { useAppNav, useSearchParam } from '@/lib/navigation'
import { Button } from '@/components/ui/button'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { ConfirmDialog, WithReason } from '@/components/dimensions/shared'
import { useHasPendingChanges, useNavigationGuard } from '@/components/shell/changes'
import { DetailHeader, DetailShell, DetailTitle, PageMessage, SectionNav, type NavGroup } from '@/components/shell/page-shell'
import { ReleaseEditScope, useLookup, useRelCall, useRelease, useReleasesApi } from '@/lib/releases/client'
import { RELEASE_TABS, type ManifestEntry, type ReleaseCheckReport, type ReleaseDetail as Detail, type ReleaseTab, type ReleaseViolation } from '@/lib/releases/contract'
import { cn } from '@/lib/utils'
import { ActivateDialog } from './activate-dialog'
import { CreateReleaseSheet } from './create-release-sheet'
import { ManifestTab } from './manifest-tab'
import { ChangesTab, ChecksPanel } from './release-tabs'
import { ActiveBadge, AbsTime, StatusBadge, useOpenTarget } from './shared'

export function ReleaseDetail({ releaseId, onBack }: { releaseId: string; onBack: () => void }) {
  const { data: detail, error } = useRelease(releaseId)
  if (error)
    return (
      <PageMessage
        action={
          <Button size="sm" variant="outline" onClick={onBack}>
            Back to releases
          </Button>
        }
      >
        This release no longer exists.
      </PageMessage>
    )
  if (!detail) return <PageMessage>Loading release…</PageMessage>
  return (
    <ReleaseEditScope releaseId={detail.release.id} label={detail.release.key}>
      <ReleasePage detail={detail} onBack={onBack} />
    </ReleaseEditScope>
  )
}

function ReleasePage({ detail, onBack }: { detail: Detail; onBack: () => void }) {
  const api = useReleasesApi()
  const lookup = useLookup()
  const nav = useAppNav()
  const { run, saving } = useRelCall()
  const [tabParam, setTabParam] = useSearchParam('tab')
  const tab: ReleaseTab = (RELEASE_TABS as readonly string[]).includes(tabParam ?? '') ? (tabParam as ReleaseTab) : 'manifest'
  const { release, isActive } = detail
  const isDraft = release.status === 'draft'
  const dirty = useHasPendingChanges()

  const [report, setReport] = useState<ReleaseCheckReport | null>(null)
  const [running, setRunning] = useState(false)
  const [highlight, setHighlight] = useState<string | null>(null)
  const [compareTo, setCompareTo] = useState<string | null>(null)
  const [dialog, setDialog] = useState<null | 'publish' | 'retire' | 'delete' | 'activate' | 'clone'>(null)
  const guard = useNavigationGuard()
  const setTab = (t: ReleaseTab) => setTabParam(t === 'manifest' ? null : t)
  const saveManifest = (manifest: ManifestEntry[]) => run(() => api.saveManifest(release.id, manifest))

  const runChecks = async () => {
    setRunning(true)
    setReport(await api.checkRelease(release.id))
    setRunning(false)
  }

  const transition = async (kind: 'validate' | 'publish') => {
    const res = await run(
      () => (kind === 'validate' ? api.validateRelease(release.id) : api.publishRelease(release.id)),
      (d) => (d.report.violations.length ? null : kind === 'validate' ? `Validated ${release.key}.` : `Published ${release.key}.`),
    )
    if (res.ok) {
      setReport(res.data.report)
      if (res.data.report.violations.length) setTab('checks')
    }
    return res
  }

  const openTarget = useOpenTarget(lookup, (datasetId) => {
    setTab('manifest')
    setHighlight(datasetId)
    setTimeout(() => setHighlight(null), 2400)
  })
  const onViolation = (v: ReleaseViolation) => {
    if (v.target) guard(() => openTarget(v.target!))
    else if (v.datasetId) openTarget({ area: 'dataset', itemId: v.datasetId })
  }

  const rowViolations = report?.violations ?? []

  const groups: NavGroup<ReleaseTab>[] = [
    {
      items: [
        { id: 'manifest', label: 'Manifest', icon: PackageIcon, badge: detail.manifest.length },
        { id: 'changes', label: 'Changes', icon: GitCompareIcon },
      ],
    },
    {
      items: [
        {
          id: 'checks',
          label: 'Checks',
          icon: ClipboardCheckIcon,
          badge: report ? (report.violations.length ? report.violations.length : 'OK') : undefined,
          tone: report ? (report.violations.length ? 'bad' : 'good') : undefined,
        },
      ],
    },
  ]

  const header = (
    <DetailHeader
      section="Releases"
      onBack={() => guard(onBack)}
      title={<DetailTitle mono>{release.key}</DetailTitle>}
      meta={
        <>
          <StatusBadge status={release.status} />
          {isActive && <ActiveBadge />}
          <span className="hidden text-xs text-muted-foreground xl:inline">
            Created <AbsTime iso={release.createdAt} /> · Published <AbsTime iso={release.publishedAt} />
            {release.contentHash && (
              <>
                {' '}
                · Hash <code className="font-mono">{release.contentHash}</code>
              </>
            )}
          </span>
        </>
      }
    >
      <LifecycleActions
        detail={detail}
        busy={saving || running}
        dirty={dirty}
        onValidate={() => transition('validate')}
        onPublish={() => setDialog('publish')}
        onBackToDraft={() => run(() => api.backToDraft(release.id), `${release.key} is a draft again.`)}
        onActivate={() => setDialog('activate')}
        onRetire={() => setDialog('retire')}
      />
      <Button variant="outline" size="sm" onClick={() => setDialog('clone')}>
        <CopyIcon />
        Clone
      </Button>
      <DropdownMenu>
          <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label="More actions" />}>
            <MoreHorizontalIcon />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-64">
            <DropdownMenuItem onClick={() => setDialog('clone')}>Clone</DropdownMenuItem>
            <DropdownMenuSeparator />
            {isDraft ? (
              <DropdownMenuItem variant="destructive" onClick={() => setDialog('delete')}>
                Delete
              </DropdownMenuItem>
            ) : (
              <DropdownMenuItem disabled className="flex-col items-start gap-0.5">
                Delete
                <span className="text-xs text-pretty text-muted-foreground">Only a draft can be deleted. Retire a published release instead.</span>
              </DropdownMenuItem>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
    </DetailHeader>
  )

  return (
    <DetailShell header={header} nav={<SectionNav label="Release sections" groups={groups} active={tab} onSelect={setTab} />}>
      <LifecycleStrip detail={detail} />

      {tab === 'manifest' ? (
        <ManifestTab manifest={detail.manifest} editable={isDraft} onChange={saveManifest} violations={rowViolations} highlight={highlight} />
      ) : tab === 'changes' ? (
        <ChangesTab detail={detail} compareTo={compareTo} onCompareTo={setCompareTo} unsaved={dirty} />
      ) : (
        <ChecksPanel report={report} running={running} onRun={runChecks} onTarget={onViolation} />
      )}

      <ConfirmDialog
        open={dialog === 'publish'}
        onOpenChange={(o) => !o && setDialog(null)}
        title={`Publish ${release.key}?`}
        confirmLabel="Publish"
        onConfirm={async () => {
          await transition('publish')
          setDialog(null)
        }}
      >
        A published release never changes; it can only be retired. Publishing does not activate it.
      </ConfirmDialog>
      <ConfirmDialog
        open={dialog === 'retire'}
        onOpenChange={(o) => !o && setDialog(null)}
        title={`Retire ${release.key}?`}
        confirmLabel="Retire"
        destructive
        onConfirm={async () => {
          const res = await run(() => api.retireRelease(release.id), `Retired ${release.key}.`)
          if (res.ok) setDialog(null)
        }}
      >
        Retiring cannot be undone; a retired release cannot be activated. Historical assumption sets that captured it keep reading it.
      </ConfirmDialog>
      <ConfirmDialog
        open={dialog === 'delete'}
        onOpenChange={(o) => !o && setDialog(null)}
        title={`Delete ${release.key}?`}
        confirmLabel="Delete"
        destructive
        onConfirm={async () => {
          const res = await run(() => api.deleteRelease(release.id), `Deleted ${release.key}.`)
          if (res.ok) {
            setDialog(null)
            onBack()
          }
        }}
      >
        The draft and its manifest are deleted.
      </ConfirmDialog>
      {dialog === 'activate' && <ActivateDialog releaseId={release.id} onClose={() => setDialog(null)} />}
      <CreateReleaseSheet
        open={dialog === 'clone'}
        copyFrom={release.id}
        onClose={() => setDialog(null)}
        onCreated={(id) => {
          setDialog(null)
          nav.release(id)
        }}
      />
    </DetailShell>
  )
}

const STEPS = ['Draft', 'Validated', 'Published', 'Active'] as const

function LifecycleStrip({ detail }: { detail: Detail }) {
  const { release, isActive } = detail
  const s = release.status
  const current = s === 'retired' ? -1 : isActive ? 3 : s === 'published' ? 2 : s === 'validated' ? 1 : 0

  return (
    <section aria-label="Lifecycle" className="flex flex-wrap items-center gap-1.5 rounded-lg border bg-card p-3">
      <ol className="flex flex-wrap items-center gap-1.5">
        {STEPS.map((label, i) => {
          const done = current > i
          const here = current === i
          return (
            <li key={label} className="flex items-center gap-1.5">
              {i > 0 && <span aria-hidden className={cn('h-px w-6', done || here ? 'bg-primary' : 'bg-border')} />}
              <span
                aria-current={here ? 'step' : undefined}
                className={cn(
                  'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs',
                  here ? 'border-primary bg-primary text-primary-foreground' : done ? 'border-primary/40 text-primary' : 'text-muted-foreground',
                )}
              >
                {done && <CheckIcon aria-hidden className="size-3" />}
                {label}
              </span>
            </li>
          )
        })}
      </ol>
      <span className="ml-2 text-sm text-muted-foreground">
        {s === 'retired'
          ? 'Retired · Retired releases cannot be activated.'
          : isActive
            ? 'Queries run on this release.'
            : s === 'draft'
              ? 'The manifest is editable.'
              : s === 'validated'
                ? 'Read-only. Go back to draft to edit.'
                : 'Published · Activate it to route queries here.'}
      </span>
    </section>
  )
}

function LifecycleActions({
  detail,
  busy,
  dirty,
  onValidate,
  onPublish,
  onBackToDraft,
  onActivate,
  onRetire,
}: {
  detail: Detail
  busy: boolean
  dirty: boolean
  onValidate: () => void
  onPublish: () => void
  onBackToDraft: () => void
  onActivate: () => void
  onRetire: () => void
}) {
  const { release, isActive } = detail
  const s = release.status

  return (
    <>
      <div className="contents">
        {s === 'draft' && (
          <WithReason reason={dirty ? 'Save the manifest before validating.' : null}>
            <Button size="sm" disabled={busy || dirty} onClick={onValidate}>
              Validate
            </Button>
          </WithReason>
        )}
        {s === 'validated' && (
          <>
            <Button size="sm" variant="outline" disabled={busy} onClick={onBackToDraft}>
              Back to draft
            </Button>
            <Button size="sm" disabled={busy} onClick={onPublish}>
              Publish
            </Button>
          </>
        )}
        {s === 'published' && (
          <>
            <WithReason reason={isActive ? 'Activate another release before retiring this one' : null}>
              <Button size="sm" variant="outline" disabled={busy || isActive} onClick={onRetire}>
                Retire
              </Button>
            </WithReason>
            {!isActive && (
              <Button size="sm" disabled={busy} onClick={onActivate}>
                Activate
              </Button>
            )}
          </>
        )}
      </div>
    </>
  )
}
