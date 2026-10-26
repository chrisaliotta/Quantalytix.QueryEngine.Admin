import { useState } from 'react'
import { toast } from 'sonner'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useEditor } from '@/lib/report/store'
import { findRow } from '@/lib/report/tree'
import { useRowActions } from '../rows/use-row-actions'

export function DeleteRowDialog({ rowKey, onClose }: { rowKey: string; onClose: () => void }) {
  const { report } = useEditor()
  const actions = useRowActions()
  const row = findRow(report.rows, rowKey)
  if (!row) return null
  const mappings = report.sources.reduce((n, s) => n + s.mappings.filter((m) => m.rowKey === rowKey).length, 0)
  const kids = row.children.length

  return (
    <AlertDialog open onOpenChange={(open) => !open && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete &ldquo;{row.label}&rdquo;?</AlertDialogTitle>
          <AlertDialogDescription>
            {kids > 0 ? `Its ${kids} ${kids === 1 ? 'row moves' : 'rows move'} up a level. ` : ''}
            {mappings > 0 ? `${mappings} ${mappings === 1 ? 'mapping' : 'mappings'} on this row will be removed. ` : ''}
            You can undo right after.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction variant="destructive" onClick={() => actions.remove(rowKey)}>
            Delete row
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

export function RenameReportDialog({ onClose }: { onClose: () => void }) {
  const { report, mutate } = useEditor()
  const [name, setName] = useState(report.settings.name)
  const invalid = !name.trim()

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <form
          className="flex flex-col gap-4"
          onSubmit={(e) => {
            e.preventDefault()
            if (invalid) return
            mutate((d) => {
              d.settings.name = name.trim()
            })
            toast('Report renamed')
            onClose()
          }}
        >
          <DialogHeader>
            <DialogTitle>Rename report</DialogTitle>
            <DialogDescription>The key stays the same, so links and schedules keep working.</DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="rename-report">Name</Label>
            <Input id="rename-report" value={name} onChange={(e) => setName(e.target.value)} autoFocus aria-invalid={invalid} />
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={invalid}>
              Rename
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

const REPORT_KEY_PATTERN = /^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)*$/

export function RenameReportKeyDialog({ onClose }: { onClose: () => void }) {
  const { report, library, activeId, mutate } = useEditor()
  const current = report.settings.key
  const [key, setKey] = useState(current)
  const next = key.trim()
  const taken = library.some((i) => i.id !== activeId && i.report.settings.key === next)
  const error = !next
    ? 'Enter a key.'
    : !REPORT_KEY_PATTERN.test(next)
      ? 'Use lowercase letters, digits, _ and dots.'
      : taken
        ? 'Another report already uses that key.'
        : null
  const unchanged = next === current

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <form
          className="flex flex-col gap-4"
          noValidate
          onSubmit={(e) => {
            e.preventDefault()
            if (error || unchanged) return
            mutate((d) => {
              d.settings.key = next
            })
            toast('Report key changed', { description: `${current} is now ${next}.` })
            onClose()
          }}
        >
          <DialogHeader>
            <DialogTitle>Rename report key</DialogTitle>
            <DialogDescription className="text-pretty">
              Links, schedules and exports that use <span className="font-mono">{current}</span> stop working until
              they point at the new key.
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="rename-report-key">Key</Label>
            <Input
              id="rename-report-key"
              value={key}
              onChange={(e) => setKey(e.target.value.replace(/\s+/g, '_').toLowerCase())}
              autoFocus
              spellCheck={false}
              autoComplete="off"
              className="font-mono"
              aria-invalid={!!error}
              aria-describedby="rename-report-key-hint"
            />
            <p id="rename-report-key-hint" className={error ? 'text-xs text-destructive' : 'text-xs text-muted-foreground'}>
              {error ?? 'Lowercase letters, digits, _ and dots, e.g. bank.net_margin'}
            </p>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={!!error || unchanged}>
              Rename key
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

export function DeleteReportDialog({ onClose }: { onClose: () => void }) {
  const { report, library, activeId, deleteReport } = useEditor()
  const last = library.length <= 1
  return (
    <AlertDialog open onOpenChange={(open) => !open && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete {report.settings.name}?</AlertDialogTitle>
          <AlertDialogDescription>
            {last
              ? 'This is the only report. Create another one before deleting it.'
              : 'This removes the report definition, its rows, sources and columns. Schedules that use it stop running. This cannot be undone.'}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction
            variant="destructive"
            disabled={last}
            onClick={() => {
              const name = report.settings.name
              onClose()
              deleteReport(activeId)
              toast(`${name} deleted`)
            }}
          >
            Delete report
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
