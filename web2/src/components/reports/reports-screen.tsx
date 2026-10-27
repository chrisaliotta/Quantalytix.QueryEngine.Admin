import { MoreHorizontalIcon, PlusIcon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { DOTTED_KEY_PATTERN } from '@/lib/api/contract'
import { EnabledBadge } from '@/components/models/model-detail'
import { ListPage } from '@/components/shell/page-shell'
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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useModels } from '@/lib/model/store'
import { useEditor, type LibraryItem } from '@/lib/report/store'
import { useAppNav } from '@/lib/navigation'


export function ReportsScreen() {
  const { library, duplicateReport } = useEditor()
  const nav = useAppNav()
  const { getModel } = useModels()
  const [creating, setCreating] = useState(false)
  const [deleting, setDeleting] = useState<LibraryItem | null>(null)

  return (
    <>
      <ListPage
        title="Reports"
        description="Report definitions, the profiles they run under and the model they read."
        actions={
          <Button onClick={() => setCreating(true)}>
            <PlusIcon /> New report
          </Button>
        }
      >
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="border-b text-xs text-muted-foreground">
              <tr className="[&>th]:h-9 [&>th]:px-3 [&>th]:text-left [&>th]:font-medium [&>th]:whitespace-nowrap">
                <th>Name</th>
                <th>Report type</th>
                <th>Periodicity</th>
                <th className="text-right!">Profiles</th>
                <th className="text-right!">Sources</th>
                <th>Status</th>
                <th>
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {library.map((item) => {
                const { settings, profiles, sources, enabled } = item.report
                return (
                  <tr
                    key={item.id}
                    onClick={() => nav.report(item.id)}
                    className="cursor-pointer hover:bg-muted/40 [&>td]:px-3 [&>td]:py-2.5"
                  >
                    <td>
                      <div className="flex items-center gap-2">
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation()
                            nav.report(item.id)
                          }}
                          className="font-medium hover:underline"
                        >
                          {settings.name}
                        </button>
                        {item.dirty && (
                          <span className="flex items-center gap-1 text-xs text-muted-foreground">
                            <span aria-hidden className="size-1.5 rounded-full bg-chart-4" />
                            Unsaved changes
                          </span>
                        )}
                      </div>
                    </td>
                    <td>{getModel(settings.model)?.name ?? settings.model}</td>
                    <td>{settings.periodicity}</td>
                    <td className="text-right tabular-nums">{profiles.length}</td>
                    <td className="text-right tabular-nums">{sources.length}</td>
                    <td>
                      <EnabledBadge enabled={enabled} />
                    </td>
                    <td className="w-10" onClick={(e) => e.stopPropagation()}>
                      <DropdownMenu>
                        <DropdownMenuTrigger
                          render={<Button variant="ghost" size="icon-sm" aria-label={`${settings.name} actions`} />}
                        >
                          <MoreHorizontalIcon />
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end" className="w-44">
                          <DropdownMenuItem onClick={() => nav.report(item.id)}>Open</DropdownMenuItem>
                          <DropdownMenuItem
                            onClick={() => {
                              duplicateReport(item.id)
                              toast(`Duplicated ${settings.name}`)
                            }}
                          >
                            Duplicate
                          </DropdownMenuItem>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem
                            variant="destructive"
                            disabled={library.length <= 1}
                            onClick={() => setDeleting(item)}
                          >
                            Delete…
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      </ListPage>
      {creating && <NewReportDialog onClose={() => setCreating(false)} />}
      {deleting && <DeleteReportConfirm item={deleting} onClose={() => setDeleting(null)} />}
    </>
  )
}

function NewReportDialog({ onClose }: { onClose: () => void }) {
  const { library, createReport } = useEditor()
  const nav = useAppNav()
  const [name, setName] = useState('')
  const [key, setKey] = useState('')
  const [from, setFrom] = useState(library[0]?.id ?? '')
  const [touched, setTouched] = useState(false)

  const taken = library.some((i) => i.report.settings.key === key)
  const keyError = !key ? 'Enter a key.' : !DOTTED_KEY_PATTERN.test(key) ? 'Start with a letter; use letters, digits, _ and dots.' : taken ? 'That key is already used.' : null
  const nameError = name.trim() ? null : 'Enter a name.'
  const valid = !keyError && !nameError

  const submit = () => {
    setTouched(true)
    if (!valid) return
    const id = createReport(from, name.trim(), key)
    onClose()
    if (id) nav.report(id)
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>New report</DialogTitle>
          <DialogDescription>The new report copies the rows, sources and profiles of the one you start from. It starts disabled.</DialogDescription>
        </DialogHeader>
        <form
          className="flex flex-col gap-4"
          onSubmit={(e) => {
            e.preventDefault()
            submit()
          }}
        >
          <div className="flex flex-col gap-2">
            <Label htmlFor="new-report-name">Name</Label>
            <Input id="new-report-name" value={name} onChange={(e) => setName(e.target.value)} autoFocus aria-invalid={touched && !!nameError} />
            {touched && nameError && <p className="text-xs text-destructive">{nameError}</p>}
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="new-report-key">Key</Label>
            <Input
              id="new-report-key"
              value={key}
              onChange={(e) => setKey(e.target.value.trim())}
              placeholder="bank.fee_income"
              className="font-mono"
              aria-invalid={touched && !!keyError}
            />
            {touched && keyError && <p className="text-xs text-destructive">{keyError}</p>}
          </div>
          <div className="flex flex-col gap-2">
            <Label>Start from</Label>
            <Select value={from} onValueChange={(v) => v && setFrom(v)}>
              <SelectTrigger className="w-full">
                <SelectValue>{(v: string) => library.find((i) => i.id === v)?.report.settings.name}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                {library.map((i) => (
                  <SelectItem key={i.id} value={i.id}>
                    {i.report.settings.name}
                    <span className="text-muted-foreground">
                      {' · '}
                      {i.report.profiles.length} {i.report.profiles.length === 1 ? 'profile' : 'profiles'}
                    </span>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit">Create report</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function DeleteReportConfirm({ item, onClose }: { item: LibraryItem; onClose: () => void }) {
  const { deleteReport } = useEditor()
  const name = item.report.settings.name
  return (
    <AlertDialog open onOpenChange={(open) => !open && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete {name}?</AlertDialogTitle>
          <AlertDialogDescription>
            This removes the report definition, its rows, sources and columns. Schedules that use it stop running. This cannot be undone.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction
            variant="destructive"
            onClick={() => {
              deleteReport(item.id)
              onClose()
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
