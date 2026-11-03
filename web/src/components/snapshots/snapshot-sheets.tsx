import { CameraIcon } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { EditorSheet, Field, ReadValue } from '@/components/dimensions/shared'
import { useCapturePreview, useSnapshotCall, useSnapshotList, useSnapshotsApi } from '@/lib/snapshots/client'
import { COUNT_OF, KIND_LABEL, SNAPSHOT_KEY_PATTERN, SNAPSHOT_KINDS, type Snapshot } from '@/lib/snapshots/contract'

export function CaptureSheet({ open, onClose, onCaptured }: { open: boolean; onClose: () => void; onCaptured: (id: string) => void }) {
  return open ? <CaptureForm onClose={onClose} onCaptured={onCaptured} /> : null
}

function CaptureForm({ onClose, onCaptured }: { onClose: () => void; onCaptured: (id: string) => void }) {
  const api = useSnapshotsApi()
  const { data: list } = useSnapshotList()
  const { data: preview } = useCapturePreview()
  const { run, saving, errorsFor, setProblem } = useSnapshotCall()
  const [key, setKey] = useState('')
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [touched, setTouched] = useState(false)

  const k = key.trim()
  const taken = list ? [list.live, ...list.frozen].some((s) => s.key === k) : false
  const keyError = !touched || !k ? null : !SNAPSHOT_KEY_PATTERN.test(k) ? 'Use a letter, then letters, digits, underscores or dots (128 at most).' : taken ? `A snapshot with key '${k}' already exists.` : null
  const nameError = touched && !name.trim() ? 'Enter a display name.' : null

  const submit = async () => {
    setTouched(true)
    if (!k || keyError || !name.trim()) return
    const res = await run(() => api.captureSnapshot({ key: k, displayName: name.trim(), description: description.trim() || null }), (s) => `Captured ${s.key}.`)
    if (res.ok) onCaptured(res.data.id)
  }

  return (
    <EditorSheet
      open
      onClose={onClose}
      dirty={!!(k || name || description)}
      title="Capture live"
      description="Copies every live version into a new frozen snapshot that requests and report comparisons can name. Live stays as it is."
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={submit} disabled={saving || !k || !name.trim()}>
            <CameraIcon />
            {saving ? 'Capturing…' : 'Capture'}
          </Button>
        </>
      }
    >
      <form
        className="flex flex-col gap-5"
        onSubmit={(e) => {
          e.preventDefault()
          submit()
        }}
      >
        <Field
          label="Key"
          htmlFor="snapshot-key"
          required
          errors={keyError ? [keyError] : errorsFor('key')}
          hint="The name requests use, such as fy2026_q3_close: a letter, then letters, digits, underscores or dots. Fixed once captured."
        >
          <Input
            id="snapshot-key"
            autoFocus
            value={key}
            onChange={(e) => {
              setKey(e.target.value)
              setProblem(null)
            }}
            onBlur={() => setTouched(true)}
            placeholder="fy2026_q3_close"
            className="font-mono"
            aria-invalid={!!keyError}
          />
        </Field>
        <Field label="Display name" htmlFor="snapshot-name" required errors={nameError ? [nameError] : errorsFor('displayName')}>
          <Input id="snapshot-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="FY2026 Q3 close" aria-invalid={!!nameError} />
        </Field>
        <Field label="Description" htmlFor="snapshot-desc" hint={`${description.length} / 1,024`}>
          <Textarea id="snapshot-desc" value={description} maxLength={1024} onChange={(e) => setDescription(e.target.value)} placeholder="What this snapshot is for" rows={3} />
        </Field>
        <div className="flex flex-col gap-1.5 rounded-md border bg-muted/40 p-3 text-sm text-pretty">
          <span className="text-xs font-medium text-muted-foreground">Copies</span>
          {preview ? (
            <ul className="grid grid-cols-2 gap-x-4 gap-y-0.5">
              {SNAPSHOT_KINDS.map((kind) => (
                <li key={kind} className="flex justify-between gap-2">
                  <span>{KIND_LABEL[kind]}</span>
                  <span className="font-mono tabular-nums">{preview[COUNT_OF[kind]]}</span>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-muted-foreground">Counting live versions…</p>
          )}
          <p className="text-xs text-muted-foreground">Report dimensions and report bridges are included. If someone saves while the capture runs, it is refused and nothing is kept.</p>
        </div>
        <button type="submit" hidden />
      </form>
    </EditorSheet>
  )
}

export function RelabelSheet({ snapshot, onClose }: { snapshot: Snapshot | null; onClose: () => void }) {
  return snapshot ? <RelabelForm key={snapshot.id} snapshot={snapshot} onClose={onClose} /> : null
}

function RelabelForm({ snapshot, onClose }: { snapshot: Snapshot; onClose: () => void }) {
  const api = useSnapshotsApi()
  const { run, saving, errorsFor } = useSnapshotCall()
  const [name, setName] = useState(snapshot.displayName)
  const [description, setDescription] = useState(snapshot.description ?? '')
  const dirty = name !== snapshot.displayName || description !== (snapshot.description ?? '')

  const submit = async () => {
    if (!name.trim()) return
    const res = await run(() => api.relabelSnapshot(snapshot.id, { displayName: name.trim(), description: description.trim() || null }), () => `Updated ${snapshot.key}.`)
    if (res.ok) onClose()
  }

  return (
    <EditorSheet
      open
      onClose={onClose}
      dirty={dirty}
      title={`Edit ${snapshot.key}`}
      description="Relabelling never changes the key or the versions the snapshot holds."
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={submit} disabled={saving || !dirty || !name.trim()}>
            {saving ? 'Saving…' : 'Save'}
          </Button>
        </>
      }
    >
      <ReadValue label="Key" mono>
        {snapshot.key}
      </ReadValue>
      <Field label="Display name" htmlFor="relabel-name" required errors={!name.trim() ? ['Enter a display name.'] : errorsFor('displayName')}>
        <Input id="relabel-name" value={name} onChange={(e) => setName(e.target.value)} />
      </Field>
      <Field label="Description" htmlFor="relabel-desc" hint={`${description.length} / 1,024`}>
        <Textarea id="relabel-desc" value={description} maxLength={1024} onChange={(e) => setDescription(e.target.value)} placeholder="What this snapshot is for" rows={3} />
      </Field>
    </EditorSheet>
  )
}
