import { CameraIcon } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { EditorSheet, Field, ReadValue } from '@/components/dimensions/shared'
import { useCapturePreview, useLookup, useRelCall, useReleasesApi, useSetList } from '@/lib/releases/client'
import { SET_KEY_PATTERN, type AssumptionSet } from '@/lib/releases/contract'

const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`

export function CaptureSheet({ open, onClose, onCaptured }: { open: boolean; onClose: () => void; onCaptured: (id: string) => void }) {
  return open ? <CaptureForm onClose={onClose} onCaptured={onCaptured} /> : null
}

function CaptureForm({ onClose, onCaptured }: { onClose: () => void; onCaptured: (id: string) => void }) {
  const api = useReleasesApi()
  const lookup = useLookup()
  const { data: sets } = useSetList()
  const { data: preview } = useCapturePreview(true)
  const { run, saving, errorsFor, setProblem } = useRelCall()
  const [key, setKey] = useState('')
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [touched, setTouched] = useState(false)

  const k = key.trim()
  const all = sets ? [sets.active, ...sets.historical] : []
  const keyError = !touched || !k ? null : !SET_KEY_PATTERN.test(k) ? 'Use a letter, then letters, digits or underscores (128 at most).' : all.some((s) => s.key === k) ? `An assumption set with key '${k}' already exists.` : null
  const nameError = touched && !name.trim() ? 'Enter a display name.' : null

  const submit = async () => {
    setTouched(true)
    if (!k || keyError || !name.trim()) return
    const res = await run(() => api.captureSet({ key: k, displayName: name.trim(), description: description.trim() || null }), (s) => `Captured ${s.key}.`)
    if (res.ok) onCaptured(res.data.id)
  }

  return (
    <EditorSheet
      open
      onClose={onClose}
      dirty={!!(k || name || description)}
      title="Capture the active set"
      description="Copies the active set's pins as they are now into a historical set that requests and report comparisons can name."
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
          htmlFor="set-key"
          required
          errors={keyError ? [keyError] : errorsFor('key')}
          hint="The name requests use, such as fy2026_q3_close: a letter, then letters, digits or underscores. Fixed once captured."
        >
          <Input
            id="set-key"
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
        <Field label="Display name" htmlFor="set-name" required errors={nameError ? [nameError] : errorsFor('displayName')}>
          <Input id="set-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="FY2026 Q3 close" aria-invalid={!!nameError} />
        </Field>
        <Field label="Description" htmlFor="set-desc" hint={`${description.length} / 1,024`}>
          <Textarea id="set-desc" value={description} maxLength={1024} onChange={(e) => setDescription(e.target.value)} placeholder="What this snapshot is for" rows={3} />
        </Field>
        <div className="flex flex-col gap-1.5 rounded-md border bg-muted/40 p-3 text-sm text-pretty">
          <span className="text-xs font-medium text-muted-foreground">Preview</span>
          {preview ? (
            <p>
              Pins {plural(preview.dimensions, 'dimension')}, {plural(preview.mappingSets, 'mapping set')} and {plural(preview.bridges, 'bridge')} as published now, and release{' '}
              <span className="font-mono font-semibold">{lookup.release(preview.releaseId)?.key}</span>. Leaves out {plural(preview.leftOutDimensions, 'report dimension')} and{' '}
              {plural(preview.leftOutBridges, 'report bridge')}, which reports always read from the active set.
            </p>
          ) : (
            <p className="text-muted-foreground">Counting pins…</p>
          )}
        </div>
        <button type="submit" hidden />
      </form>
    </EditorSheet>
  )
}

export function RelabelSheet({ set, onClose }: { set: AssumptionSet | null; onClose: () => void }) {
  return set ? <RelabelForm key={set.id} set={set} onClose={onClose} /> : null
}

function RelabelForm({ set, onClose }: { set: AssumptionSet; onClose: () => void }) {
  const api = useReleasesApi()
  const { run, saving, errorsFor } = useRelCall()
  const [name, setName] = useState(set.displayName)
  const [description, setDescription] = useState(set.description ?? '')
  const dirty = name !== set.displayName || description !== (set.description ?? '')

  const submit = async () => {
    if (!name.trim()) return
    const res = await run(() => api.relabelSet(set.id, { displayName: name.trim(), description: description.trim() || null }), `Updated ${set.key}.`)
    if (res.ok) onClose()
  }

  return (
    <EditorSheet
      open
      onClose={onClose}
      dirty={dirty}
      title={`Edit ${set.key}`}
      description="Relabelling does not change what the set pins or which release it reads."
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
        {set.key}
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
