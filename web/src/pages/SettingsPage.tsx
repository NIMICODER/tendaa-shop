import { useState } from 'react'
import { Pencil, Plus, Trash2, X } from 'lucide-react'
import {
  useCreateZone,
  useDeleteZone,
  useSettings,
  useUpdateSettings,
  useUpdateZone,
  useZones,
  type DeliveryZoneSummary,
  type ShopSettings,
} from '@/api/settings'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Textarea } from '@/components/ui/textarea'

const blank = (value: string) => (value.trim() === '' ? null : value.trim())

export function SettingsPage() {
  return (
    <div className="flex max-w-3xl flex-col gap-4 p-8">
      <h1 className="text-2xl font-bold">Settings</h1>
      <ShopCard />
      <ZonesCard />
    </div>
  )
}

function ShopCard() {
  const settings = useSettings()

  return (
    <Card>
      <CardHeader>
        <CardTitle>Shop</CardTitle>
      </CardHeader>
      <CardContent>
        {settings.isPending ? (
          <p className="text-sm text-muted-foreground">Loading…</p>
        ) : settings.isError ? (
          <p className="text-sm text-muted-foreground">{settings.error.message}</p>
        ) : (
          <ShopForm key={settings.data.shopId} initial={settings.data} />
        )}
      </CardContent>
    </Card>
  )
}

function ShopForm({ initial }: { initial: ShopSettings }) {
  const update = useUpdateSettings()
  const [form, setForm] = useState({
    name: initial.name,
    country: initial.country,
    currency: initial.currency,
    timeZone: initial.timeZone,
    hoursText: initial.hoursText ?? '',
    rulesText: initial.rulesText ?? '',
    bankName: initial.bankName ?? '',
    bankAccountNumber: initial.bankAccountNumber ?? '',
    bankAccountName: initial.bankAccountName ?? '',
  })

  function set<K extends keyof typeof form>(key: K, value: string) {
    setForm((f) => ({ ...f, [key]: value }))
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="grid grid-cols-2 gap-3">
        <Field label="Shop name">
          <Input value={form.name} onChange={(e) => set('name', e.target.value)} />
        </Field>
        <Field label="Country">
          <Input value={form.country} onChange={(e) => set('country', e.target.value)} />
        </Field>
        <Field label="Currency">
          <Input value={form.currency} onChange={(e) => set('currency', e.target.value)} />
        </Field>
        <Field label="Time zone">
          <Input value={form.timeZone} onChange={(e) => set('timeZone', e.target.value)} />
        </Field>
      </div>
      <Field label="Opening hours">
        <Input value={form.hoursText} onChange={(e) => set('hoursText', e.target.value)} />
      </Field>
      <Field label="Shop rules">
        <Textarea value={form.rulesText} onChange={(e) => set('rulesText', e.target.value)} />
      </Field>
      <div className="grid grid-cols-3 gap-3">
        <Field label="Bank name">
          <Input value={form.bankName} onChange={(e) => set('bankName', e.target.value)} />
        </Field>
        <Field label="Account number">
          <Input
            value={form.bankAccountNumber}
            onChange={(e) => set('bankAccountNumber', e.target.value)}
          />
        </Field>
        <Field label="Account name">
          <Input
            value={form.bankAccountName}
            onChange={(e) => set('bankAccountName', e.target.value)}
          />
        </Field>
      </div>
      {update.error && (
        <p className="text-sm font-medium text-destructive">{update.error.message}</p>
      )}
      {update.data?.isSuccessful && (
        <p className="text-sm font-medium text-status-positive">Saved.</p>
      )}
      <div>
        <Button
          disabled={update.isPending}
          onClick={() =>
            update.mutate({
              name: form.name.trim(),
              country: form.country.trim(),
              currency: form.currency.trim(),
              timeZone: form.timeZone.trim(),
              hoursText: blank(form.hoursText),
              rulesText: blank(form.rulesText),
              bankName: blank(form.bankName),
              bankAccountNumber: blank(form.bankAccountNumber),
              bankAccountName: blank(form.bankAccountName),
            })
          }
        >
          {update.isPending ? 'Saving…' : 'Save changes'}
        </Button>
      </div>
    </div>
  )
}

function ZonesCard() {
  const zones = useZones()
  const create = useCreateZone()
  const update = useUpdateZone()
  const remove = useDeleteZone()
  const [adding, setAdding] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [draft, setDraft] = useState({ name: '', fee: '', deliveryTimeText: '' })

  function startAdd() {
    setAdding(true)
    setEditingId(null)
    setDraft({ name: '', fee: '', deliveryTimeText: '' })
  }

  function startEdit(zone: DeliveryZoneSummary) {
    setAdding(false)
    setEditingId(zone.id)
    setDraft({ name: zone.name, fee: String(zone.fee), deliveryTimeText: zone.deliveryTimeText })
  }

  function cancel() {
    setAdding(false)
    setEditingId(null)
  }

  function save() {
    const fee = Number(draft.fee)
    if (draft.name.trim().length === 0 || !Number.isFinite(fee) || fee < 0) {
      return
    }
    const input = {
      name: draft.name.trim(),
      fee,
      deliveryTimeText: blank(draft.deliveryTimeText),
    }
    if (adding) {
      create.mutate(input, { onSuccess: (r) => r.isSuccessful && cancel() })
    } else if (editingId) {
      update.mutate({ zoneId: editingId, input }, { onSuccess: (r) => r.isSuccessful && cancel() })
    }
  }

  function destroy(zone: DeliveryZoneSummary) {
    if (window.confirm(`Delete the "${zone.name}" delivery zone?`)) {
      remove.mutate(zone.id)
    }
  }

  const error =
    create.error?.message ?? update.error?.message ?? remove.error?.message ?? null
  const busy = create.isPending || update.isPending || remove.isPending

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle>Delivery zones</CardTitle>
          {!adding && (
            <Button size="sm" onClick={startAdd}>
              <Plus /> Add zone
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent>
        {zones.isPending ? (
          <p className="text-sm text-muted-foreground">Loading…</p>
        ) : zones.isError ? (
          <p className="text-sm text-muted-foreground">{zones.error.message}</p>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Zone</TableHead>
                <TableHead>Fee</TableHead>
                <TableHead>Delivery time</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {zones.data.map((z) =>
                editingId === z.id ? (
                  <TableRow key={z.id}>
                    <TableCell>
                      <Input
                        value={draft.name}
                        onChange={(e) => setDraft((d) => ({ ...d, name: e.target.value }))}
                      />
                    </TableCell>
                    <TableCell>
                      <Input
                        value={draft.fee}
                        inputMode="decimal"
                        onChange={(e) => setDraft((d) => ({ ...d, fee: e.target.value }))}
                      />
                    </TableCell>
                    <TableCell>
                      <Input
                        value={draft.deliveryTimeText}
                        onChange={(e) =>
                          setDraft((d) => ({ ...d, deliveryTimeText: e.target.value }))
                        }
                      />
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-1">
                        <Button size="sm" onClick={save} disabled={busy}>
                          Save
                        </Button>
                        <button
                          type="button"
                          title="Cancel"
                          onClick={cancel}
                          className="flex h-8 w-8 items-center justify-center rounded-lg text-muted-foreground hover:bg-muted"
                        >
                          <X className="size-4" />
                        </button>
                      </div>
                    </TableCell>
                  </TableRow>
                ) : (
                  <TableRow key={z.id}>
                    <TableCell className="font-semibold">{z.name}</TableCell>
                    <TableCell>{z.formattedFee}</TableCell>
                    <TableCell>{z.deliveryTimeText}</TableCell>
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-1">
                        <button
                          type="button"
                          title="Edit"
                          onClick={() => startEdit(z)}
                          className="flex h-8 w-8 items-center justify-center rounded-lg text-muted-foreground hover:bg-muted"
                        >
                          <Pencil className="size-4" />
                        </button>
                        <button
                          type="button"
                          title="Delete"
                          onClick={() => destroy(z)}
                          className="flex h-8 w-8 items-center justify-center rounded-lg text-muted-foreground hover:bg-muted"
                        >
                          <Trash2 className="size-4" />
                        </button>
                      </div>
                    </TableCell>
                  </TableRow>
                ),
              )}
              {adding && (
                <TableRow>
                  <TableCell>
                    <Input
                      placeholder="Zone name"
                      value={draft.name}
                      onChange={(e) => setDraft((d) => ({ ...d, name: e.target.value }))}
                    />
                  </TableCell>
                  <TableCell>
                    <Input
                      placeholder="Fee"
                      inputMode="decimal"
                      value={draft.fee}
                      onChange={(e) => setDraft((d) => ({ ...d, fee: e.target.value }))}
                    />
                  </TableCell>
                  <TableCell>
                    <Input
                      placeholder="Delivery time"
                      value={draft.deliveryTimeText}
                      onChange={(e) => setDraft((d) => ({ ...d, deliveryTimeText: e.target.value }))}
                    />
                  </TableCell>
                  <TableCell className="text-right">
                    <div className="flex justify-end gap-1">
                      <Button size="sm" onClick={save} disabled={busy}>
                        Save
                      </Button>
                      <button
                        type="button"
                        title="Cancel"
                        onClick={cancel}
                        className="flex h-8 w-8 items-center justify-center rounded-lg text-muted-foreground hover:bg-muted"
                      >
                        <X className="size-4" />
                      </button>
                    </div>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        )}
        {error && <p className="mt-2 text-sm font-medium text-destructive">{error}</p>}
      </CardContent>
    </Card>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="flex flex-col gap-1.5 text-sm font-medium">
      {label}
      {children}
    </label>
  )
}
