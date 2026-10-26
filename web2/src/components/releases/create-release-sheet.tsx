import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { SimpleSelect } from '@/components/report/shared/simple-select'
import { EditorSheet, Field } from '@/components/dimensions/shared'
import { useLookup, useRelCall, useReleaseList, useReleasesApi } from '@/lib/releases/client'
import { RELEASE_KEY_PATTERN } from '@/lib/releases/contract'

const EMPTY = '__empty__'

export function CreateReleaseSheet({ open, copyFrom, onClose, onCreated }: { open: boolean; copyFrom: string | null; onClose: () => void; onCreated: (id: string) => void }) {
  return open ? <CreateForm key={copyFrom ?? EMPTY} copyFrom={copyFrom} onClose={onClose} onCreated={onCreated} /> : null
}

function CreateForm({ copyFrom, onClose, onCreated }: { copyFrom: string | null; onClose: () => void; onCreated: (id: string) => void }) {
  const api = useReleasesApi()
  const { data: list } = useReleaseList()
  const lookup = useLookup()
  const { run, saving, errorsFor, setProblem } = useRelCall()
  const [key, setKey] = useState('')
  const [source, setSource] = useState<string | null>(copyFrom)
  const [touched, setTouched] = useState(false)

  const k = key.trim()
  const localError = !touched || !k ? null : !RELEASE_KEY_PATTERN.test(k) ? 'Use letters, digits, _ . - /, starting with a letter or digit (128 at most).' : list?.some((r) => r.key === k) ? `A release with key '${k}' already exists.` : null
  const errors = localError ? [localError] : errorsFor('key')
  const src = source ? list?.find((r) => r.id === source) : null
  const cloned = copyFrom && lookup.release(copyFrom)

  const submit = async () => {
    setTouched(true)
    if (!k || localError) return
    const res = await run(() => api.createRelease({ key: k, copyFromReleaseId: source }), (r) => `Created draft ${r.key}.`)
    if (res.ok) onCreated(res.data.id)
  }

  return (
    <EditorSheet
      open
      onClose={onClose}
      dirty={!!k}
      title={cloned && copyFrom !== lookup.catalog?.tenant.activeReleaseId ? `New release from ${cloned.key}` : 'New release'}
      description="A new release starts as a draft. Move datasets to new versions, validate, publish, then activate."
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={submit} disabled={saving || !k}>
            {saving ? 'Creating…' : 'Create draft'}
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
          htmlFor="release-key"
          required
          errors={errors}
          hint={
            <>
              Letters, digits, <code className="font-mono">_ . - /</code>, starting with a letter or digit, such as prod/2026-q4. Fixed once created.
            </>
          }
        >
          <Input
            id="release-key"
            autoFocus
            value={key}
            onChange={(e) => {
              setKey(e.target.value)
              setProblem(null)
            }}
            onBlur={() => setTouched(true)}
            placeholder="prod/2026-q4"
            className="font-mono"
            aria-invalid={!!errors?.length}
          />
        </Field>
        <Field label="Copy manifest from" htmlFor="release-source" hint="Starts from this release's pinned dataset versions; clear it to start empty.">
          <SimpleSelect
            id="release-source"
            value={source ?? EMPTY}
            onChange={(v) => setSource(v === EMPTY ? null : v)}
            options={[
              { value: EMPTY, label: 'Start empty' },
              ...(list ?? []).map((r) => ({ value: r.id, label: r.isActive ? `${r.key} (active)` : r.key, hint: r.status })),
            ]}
          />
        </Field>
        <p className="rounded-md border bg-muted/40 p-3 text-sm text-pretty">
          Creates draft <span className="font-mono font-semibold">{k || '…'}</span>{' '}
          {src ? (
            <>
              pinning the {src.datasetCount} dataset versions of <span className="font-mono">{src.key}</span>.
            </>
          ) : (
            'with an empty manifest.'
          )}
        </p>
        <button type="submit" hidden />
      </form>
    </EditorSheet>
  )
}
