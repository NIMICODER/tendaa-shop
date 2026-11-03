import { useState } from 'react'
import { Send } from 'lucide-react'
import { useHandoffDetail, useHandoffs, useResolveHandoff, type HandoffStatus } from '@/api/handoffs'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Textarea } from '@/components/ui/textarea'
import { formatAge } from '@/lib/format'
import { cn } from '@/lib/utils'

export function HandoffsPage() {
  const [tab, setTab] = useState<HandoffStatus>('Open')
  const [selectedIdState, setSelectedId] = useState<string | undefined>()
  const list = useHandoffs(tab)
  const resolve = useResolveHandoff()
  const [reply, setReply] = useState('')

  // Derived during render, not synced in an effect: explicit selection wins, otherwise
  // the first item. Switching tabs clears back to undefined via selectTab.
  const selectedId =
    selectedIdState ?? (list.data && list.data.length > 0 ? list.data[0].handoffId : undefined)
  const detail = useHandoffDetail(selectedId)

  function selectTab(next: HandoffStatus) {
    setTab(next)
    setSelectedId(undefined)
    setReply('')
    resolve.reset()
  }

  function submit(withReply: boolean) {
    if (!selectedId || resolve.isPending) {
      return
    }
    resolve.mutate(
      { handoffId: selectedId, replyText: withReply ? reply.trim() : '' },
      { onSuccess: () => setReply('') },
    )
  }

  return (
    <div className="flex h-screen flex-col p-8 pb-0">
      <h1 className="text-2xl font-bold">Handoffs</h1>

      <div className="mt-4 flex gap-2">
        {(['Open', 'Resolved'] as const).map((t) => (
          <button
            key={t}
            type="button"
            onClick={() => selectTab(t)}
            className={cn(
              'rounded-full px-4 py-1.5 text-sm font-medium text-muted-foreground hover:bg-muted',
              tab === t && 'bg-accent font-semibold text-accent-foreground hover:bg-accent',
            )}
          >
            {t}
          </button>
        ))}
      </div>

      <div className="mt-4 flex min-h-0 flex-1 gap-4 pb-8">
        <Card className="flex w-80 shrink-0 flex-col overflow-hidden">
          <div className="min-h-0 flex-1 overflow-auto">
            {list.isPending ? (
              <p className="p-4 text-sm text-muted-foreground">Loading…</p>
            ) : list.isError ? (
              <p className="p-4 text-sm text-muted-foreground">{list.error.message}</p>
            ) : list.data.length === 0 ? (
              <p className="p-4 text-sm text-muted-foreground">Nothing here.</p>
            ) : (
              list.data.map((h) => (
                <button
                  key={h.handoffId}
                  type="button"
                  onClick={() => {
                    setSelectedId(h.handoffId)
                    setReply('')
                    resolve.reset()
                  }}
                  className={cn(
                    'flex w-full flex-col gap-1 border-b border-border p-4 text-left hover:bg-muted/50',
                    selectedId === h.handoffId && 'bg-accent hover:bg-accent',
                  )}
                >
                  <div className="flex items-center justify-between gap-2">
                    <p className="truncate text-sm font-semibold">{h.customerName}</p>
                    <span className="shrink-0 text-xs text-muted-foreground">
                      {formatAge(h.createdAt)}
                    </span>
                  </div>
                  <p className="truncate text-[13px] text-muted-foreground">{h.reason}</p>
                </button>
              ))
            )}
          </div>
        </Card>

        <Card className="flex min-h-0 flex-1 flex-col overflow-hidden">
          {detail.isPending || selectedId === undefined ? (
            <p className="p-6 text-sm text-muted-foreground">
              {selectedId === undefined ? 'Select a handoff.' : 'Loading…'}
            </p>
          ) : detail.isError ? (
            <p className="p-6 text-sm text-muted-foreground">{detail.error.message}</p>
          ) : (
            <>
              <div className="border-b border-border p-6">
                <div className="flex items-center gap-2">
                  <h2 className="text-lg font-bold">{detail.data.customerName}</h2>
                  <Badge variant={detail.data.status === 'Open' ? 'default' : 'success'}>
                    {detail.data.status}
                  </Badge>
                </div>
                <p className="mt-1 text-sm text-muted-foreground">
                  {detail.data.customerPhone ?? 'Phone hidden'} · {detail.data.reason}
                </p>
                <p className="mt-2 text-sm">{detail.data.summary}</p>
              </div>

              <div className="flex min-h-0 flex-1 flex-col gap-2 overflow-auto p-6">
                {detail.data.recentMessages.map((m, i) => (
                  <div
                    key={`${m.createdAt}-${i}`}
                    className={cn(
                      'max-w-[80%] rounded-2xl px-3.5 py-2 text-sm',
                      m.direction === 'Inbound'
                        ? 'self-start bg-muted'
                        : 'self-end bg-accent text-accent-foreground',
                    )}
                  >
                    {m.text ?? '—'}
                  </div>
                ))}
              </div>

              <div className="border-t border-border p-4">
                {resolve.data?.message && (
                  <p className="mb-2 text-sm font-medium">{resolve.data.message}</p>
                )}
                {resolve.error && (
                  <p className="mb-2 text-sm font-medium text-destructive">{resolve.error.message}</p>
                )}
                {detail.data.status === 'Open' ? (
                  <div className="flex flex-col gap-2">
                    <Textarea
                      placeholder="Reply to the customer over WhatsApp…"
                      value={reply}
                      onChange={(e) => setReply(e.target.value)}
                    />
                    <div className="flex gap-2">
                      <Button
                        onClick={() => submit(true)}
                        disabled={resolve.isPending || reply.trim().length === 0}
                      >
                        <Send /> {resolve.isPending ? 'Resolving…' : 'Reply & resolve'}
                      </Button>
                      <Button
                        variant="ghost"
                        onClick={() => submit(false)}
                        disabled={resolve.isPending}
                      >
                        Resolve silently
                      </Button>
                    </div>
                  </div>
                ) : (
                  <p className="text-sm text-muted-foreground">
                    Resolved{detail.data.resolvedBy ? ` by ${detail.data.resolvedBy}` : ''}.
                  </p>
                )}
              </div>
            </>
          )}
        </Card>
      </div>
    </div>
  )
}
