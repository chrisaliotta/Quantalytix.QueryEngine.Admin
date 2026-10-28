import { PlusIcon, Trash2Icon } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { AddRow, InlineText, focusRowClass, keyFromName, type AddRowField } from '@/components/shell/inline-table'
import { Button } from '@/components/ui/button'
import { useDimensionsApi, useRefreshDimensions } from '@/lib/dimensions/client'
import { KEY_HELP, type DimensionRole, type DimensionUsageKind } from '@/lib/dimensions/contract'
import type { SectionProps } from './dimension-detail'
import { DeleteDialog, EmptyState, Mono, RequiredLock, TabIntro, Td, Th, WithReason, useDimCall } from './shared'

const REPORT_KINDS = new Set<DimensionUsageKind>(['calculation_dimension', 'report_scenario', 'report_source'])

export function RolesSection({ dim, editable, policy, focusId, clearFocus }: SectionProps) {
  const api = useDimensionsApi()
  const refresh = useRefreshDimensions()
  const { run } = useDimCall()
  const [highlight, setHighlight] = useState<string | null>(null)
  const [deleting, setDeleting] = useState<DimensionRole | null>(null)
  const addRef = useRef<HTMLTableSectionElement>(null)
  const required = new Set(dim.profile?.requiredRoleKeys ?? [])
  const singleRole = !!dim.profile?.singleRole
  // Roles belong to the dimension, so they follow the policy rather than the selected version's status.
  const canEdit = policy !== 'locked'
  const canAdd = canEdit && !singleRole
  const bound = (r: DimensionRole) => dim.usage.filter((u) => u.roleKey === r.key && u.kind === 'dataset_binding').length
  const reports = (r: DimensionRole) => dim.usage.filter((u) => u.roleKey === r.key && REPORT_KINDS.has(u.kind)).length

  useEffect(() => {
    if (!focusId) return
    if (dim.roles.some((x) => x.id === focusId)) {
      setHighlight(focusId)
      requestAnimationFrame(() => document.getElementById(`role-${focusId}`)?.scrollIntoView({ block: 'center' }))
    }
    clearFocus()
  }, [focusId, dim.roles, clearFocus])

  async function patch(r: DimensionRole, changes: Partial<Pick<DimensionRole, 'displayName' | 'description'>>) {
    await run(() => api.saveRole(dim.id, { id: r.id, key: r.key, displayName: r.displayName, description: r.description, ...changes }))
  }

  async function add(v: Record<string, string>) {
    const r = await api.saveRole(dim.id, { key: v.key, displayName: v.displayName, description: v.description || null })
    if (!r.ok) return Object.keys(r.problem.errors).length ? r.problem.errors : { '': [r.problem.detail ?? r.problem.title] }
    await refresh()
  }

  const addFields: AddRowField[] = [
    { name: 'displayName', placeholder: 'Add role — name', required: true },
    { name: 'key', placeholder: 'key', mono: true, required: true, derive: (v) => keyFromName(v.displayName ?? '', 'lower') },
    { name: 'description', placeholder: 'Description (optional)' },
  ]

  return (
    <div className="flex flex-col gap-4">
      <TabIntro
        action={
          canEdit && (
            <WithReason reason={singleRole ? `The ${dim.profile!.displayName} profile allows exactly one role.` : null}>
              <Button size="sm" disabled={singleRole} onClick={() => addRef.current?.querySelector('input')?.focus()}>
                <PlusIcon /> Add role
              </Button>
            </WithReason>
          )
        }
      >
        A role is a named use of this dimension. Datasets bind fields to a role, and queries group by the role&apos;s key. Example: Servicing branch and Origination branch are both roles of Branch.
      </TabIntro>
      {!editable && canEdit && <p className="text-xs text-muted-foreground">Roles belong to the dimension, not a version, so they stay editable.</p>}
      {!dim.roles.length && !canAdd ? (
        <EmptyState title="No roles">Add a role so datasets can bind fields to this dimension.</EmptyState>
      ) : (
        <div className="overflow-x-auto rounded-lg border bg-card">
          <table className="w-full text-sm">
            <thead className="border-b bg-muted/40">
              <tr>
                <Th>Display name</Th>
                <Th>Key</Th>
                <Th>Description</Th>
                <Th className="text-right">Bound by</Th>
                <Th className="text-right">Reports</Th>
                <Th className="w-20">
                  <span className="sr-only">Actions</span>
                </Th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {dim.roles.map((r) => (
                <tr key={r.id} id={`role-${r.id}`} onClick={() => highlight && highlight !== r.id && setHighlight(null)} className={focusRowClass(highlight === r.id)}>
                  <Td>{canEdit ? <InlineText aria-label={`Name of role ${r.key}`} value={r.displayName} onCommit={(v) => v && v !== r.displayName && patch(r, { displayName: v })} className="-ml-2" /> : r.displayName}</Td>
                  <Td>
                    <span className="flex items-center gap-1.5">
                      <Mono>{r.key}</Mono>
                      {required.has(r.key) && <RequiredLock what="Required role" />}
                    </span>
                  </Td>
                  <Td className="max-w-96">
                    {canEdit ? (
                      <InlineText aria-label={`Description of ${r.displayName}`} value={r.description ?? ''} placeholder="Add a description" onCommit={(v) => (v || null) !== r.description && patch(r, { description: v || null })} className="-ml-2 text-muted-foreground" />
                    ) : (
                      <span className="block truncate text-muted-foreground">{r.description ?? '—'}</span>
                    )}
                  </Td>
                  <Td className="text-right tabular-nums">{bound(r)}</Td>
                  <Td className="text-right tabular-nums">{reports(r)}</Td>
                  <Td className="text-right whitespace-nowrap">
                    {canEdit && !(policy === 'extend' && required.has(r.key)) && (
                      <Button variant="ghost" size="icon-xs" aria-label={`Delete ${r.displayName}`} onClick={() => setDeleting(r)} className="opacity-0 group-hover:opacity-100 focus-visible:opacity-100">
                        <Trash2Icon />
                      </Button>
                    )}
                  </Td>
                </tr>
              ))}
            </tbody>
            {canAdd && (
              <tbody ref={addRef}>
                <AddRow label="New role" fields={addFields} onSubmit={add} trailingCells={3} hint={KEY_HELP} />
              </tbody>
            )}
          </table>
        </div>
      )}
      <DeleteDialog
        target={deleting ? { type: 'role', roleId: deleting.id } : null}
        title={`Delete role ${deleting?.displayName ?? ''}?`}
        onOpenChange={(o) => !o && setDeleting(null)}
        onConfirm={async () => (deleting ? (await run(() => api.deleteRole(deleting.id), `Deleted role ${deleting.displayName}`)).ok : false)}
      />
    </div>
  )
}
