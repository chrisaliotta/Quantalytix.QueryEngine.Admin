import { KeyRoundIcon, MoreHorizontalIcon, PencilIcon, Trash2Icon } from 'lucide-react'
import { toast } from 'sonner'
import { DetailHeader, DetailKey } from '@/components/shell/page-shell'
import { useRegisterChangeScope } from '@/components/shell/changes'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import { useEffect, useEffectEvent } from 'react'
import { useEditor } from '@/lib/report/store'
import { useAppNav } from '@/lib/navigation'
import { useModels } from '@/lib/model/store'
import { usePublishStatus } from './settings/publish-section'
import { KeyText } from './shared/bits'
import { EditableLabel } from './shared/editable-label'

function ReportFacts() {
  const { report, setUi, openDialog } = useEditor()
  const { getModel } = useModels()
  const { settings, profiles } = report
  const model = getModel(settings.model)
  const defaultProfile = profiles.find((p) => p.key === settings.defaultProfile)

  return (
    <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-2">
      <div className="flex items-center gap-1.5 text-sm">
        <span className="text-muted-foreground">Type</span>
        <span className="font-medium">{model?.name ?? settings.model}</span>
        <Button
          variant="ghost"
          size="icon-sm"
          aria-label="Edit report type"
          title="Edit report type"
          onClick={() => openDialog({ type: 'changeModel' })}
        >
          <PencilIcon aria-hidden />
        </Button>
      </div>
      {profiles.length > 1 && defaultProfile && (
        <Button
          variant="ghost"
          size="sm"
          className="text-muted-foreground"
          title="Change the default on the Profiles tab"
          onClick={() => setUi({ view: 'settings', settingsSection: 'profiles' })}
        >
          Default profile
          <span className="font-medium text-foreground">{defaultProfile.name}</span>
        </Button>
      )}
    </div>
  )
}

function EnabledToggle({
  canEnable,
  blockingCount,
  onReview,
}: {
  canEnable: boolean
  blockingCount: number
  onReview: () => void
}) {
  const { report, saved, mutate } = useEditor()
  const changed = report.enabled !== saved.enabled
  const blocked = report.enabled && !canEnable

  return (
    <div className="flex items-center gap-2">
      <Switch
        id="report-enabled"
        size="sm"
        checked={report.enabled}
        disabled={!report.enabled && !canEnable}
        aria-describedby={!canEnable ? 'report-enabled-blocking' : undefined}
        onCheckedChange={(next) =>
          mutate((d) => {
            d.enabled = next
          })
        }
      />
      <Label
        htmlFor="report-enabled"
        className={report.enabled ? 'text-foreground' : 'text-muted-foreground'}
        title={
          report.enabled
            ? 'People can find and run this report'
            : canEnable
              ? 'Hidden from the report list'
              : 'Fix the blocking diagnostics to enable'
        }
      >
        {report.enabled ? 'Enabled' : 'Disabled'}
        {changed && (
          <span className="sr-only"> (takes effect when you save)</span>
        )}
        {changed && <span aria-hidden className="size-1.5 rounded-full bg-warning" title="Takes effect when you save" />}
      </Label>
      {!canEnable && (
        <Button
          id="report-enabled-blocking"
          variant="ghost"
          size="xs"
          className={blocked ? 'text-destructive' : 'text-muted-foreground'}
          onClick={onReview}
        >
          {blockingCount} blocking {blockingCount === 1 ? 'check' : 'checks'}
        </Button>
      )}
    </div>
  )
}

export function ReportHeader() {
  const { report, saved, savedAt, dirty, save, discard, setReport, ui, setUi, openDialog, mutate } = useEditor()
  const nav = useAppNav()
  const { settings } = report
  const publish = usePublishStatus()
  const goPublish = () => setUi({ view: 'settings', settingsSection: 'publish' })

  useRegisterChangeScope({
    id: 'report-editor',
    label: settings.name,
    count: dirty ? 1 : 0,
    saving: false,
    save: async () => commit(),
    discard: async () => discard(),
  })

  const handleSave = () => void commit()
  const onSaveShortcut = useEffectEvent(handleSave)

  async function commit() {
    if (!dirty) return true
    if (report.enabled && !publish.canEnable) {
      toast.error("Can't save an enabled report with failing checks", {
        description: 'Fix the blocking diagnostics or disable the report first.',
        action: { label: 'Open Diagnostics', onClick: goPublish },
      })
      goPublish()
      return false
    }
    const enabledChanged = report.enabled !== saved.enabled
    const refusal = await save()
    if (refusal) {
      toast.error("Couldn't save the report", { description: refusal.join(' ') })
      return false
    }
    toast.success('Changes saved', {
      description: enabledChanged
        ? report.enabled
          ? `${settings.name} is now available to run.`
          : `${settings.name} is hidden from the report list.`
        : undefined,
    })
    return true
  }

  const handleDiscard = () => {
    const previous = report
    discard()
    toast('Changes discarded', {
      action: { label: 'Undo', onClick: () => setReport(previous) },
    })
  }

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 's') {
        e.preventDefault()
        onSaveShortcut()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  return (
    <DetailHeader
      section="Reports"
      onBack={nav.reports}
      title={
        <h1 className="min-w-0 max-w-sm">
          <EditableLabel
            aria-label="Report name"
            value={settings.name}
            onCommit={(v) => {
              if (!v) return
              mutate((d) => {
                d.settings.name = v
              })
            }}
            showIcon
            className="h-8 w-auto max-w-full text-sm font-semibold"
          />
        </h1>
      }
      meta={<DetailKey>{settings.key}</DetailKey>}
    >
      <ReportFacts />
      <span aria-hidden className="hidden h-4 w-px bg-border lg:block" />
      <EnabledToggle canEnable={publish.canEnable} blockingCount={publish.ready.issues.length} onReview={goPublish} />
      <span aria-hidden className="hidden h-4 w-px bg-border lg:block" />
        <div className="flex items-center gap-2">
          <div className="flex items-center gap-2" aria-live="polite">
            {dirty ? (
              <>
                <span className="flex items-center gap-1.5 text-sm text-muted-foreground">
                  <span aria-hidden className="size-2 rounded-full bg-warning" />
                  Unsaved changes
                </span>
                <Button variant="ghost" size="sm" onClick={handleDiscard}>
                  Discard
                </Button>
                <Button size="sm" onClick={handleSave}>
                  Save
                  <kbd className="ml-1 hidden font-sans text-xs opacity-70 sm:inline">⌘S</kbd>
                </Button>
              </>
            ) : (
              <span className="text-sm text-muted-foreground">
                {savedAt ? `Saved ${savedAt.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}` : 'All changes saved'}
              </span>
            )}
          </div>
          <DropdownMenu>
            <DropdownMenuTrigger render={<Button variant="ghost" size="icon-sm" aria-label="Report actions" />}>
              <MoreHorizontalIcon />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-60">
              <DropdownMenuCheckboxItem checked={ui.advanced} onCheckedChange={(v) => setUi({ advanced: v })}>
                Advanced mode
              </DropdownMenuCheckboxItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={() => openDialog({ type: 'renameReportKey' })}>
                <KeyRoundIcon />
                <span className="flex min-w-0 flex-1 items-baseline justify-between gap-3">
                  Rename report key…
                  <KeyText className="truncate text-xs">{settings.key}</KeyText>
                </span>
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem variant="destructive" onClick={() => openDialog({ type: 'deleteReport' })}>
                <Trash2Icon />
                Delete report…
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
    </DetailHeader>
  )
}
