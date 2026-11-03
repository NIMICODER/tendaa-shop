import { useState } from 'react'
import { useConversationThread, useConversations, type ThreadMessage } from '@/api/conversations'
import { Card } from '@/components/ui/card'
import { formatAge } from '@/lib/format'
import { cn } from '@/lib/utils'

export function ConversationsPage() {
  const [selectedIdState, setSelectedId] = useState<string | undefined>()
  const list = useConversations()

  // Derived during render, not synced in an effect: explicit selection wins, otherwise
  // the first conversation.
  const selectedId =
    selectedIdState ?? (list.data && list.data.length > 0 ? list.data[0].conversationId : undefined)
  const thread = useConversationThread(selectedId)

  return (
    <div className="flex h-screen flex-col p-8 pb-0">
      <h1 className="text-2xl font-bold">Conversations</h1>

      <div className="mt-4 flex min-h-0 flex-1 gap-4 pb-8">
        <Card className="flex w-80 shrink-0 flex-col overflow-hidden">
          <div className="min-h-0 flex-1 overflow-auto">
            {list.isPending ? (
              <p className="p-4 text-sm text-muted-foreground">Loading…</p>
            ) : list.isError ? (
              <p className="p-4 text-sm text-muted-foreground">{list.error.message}</p>
            ) : list.data.length === 0 ? (
              <p className="p-4 text-sm text-muted-foreground">No conversations yet.</p>
            ) : (
              list.data.map((c) => (
                <button
                  key={c.conversationId}
                  type="button"
                  onClick={() => setSelectedId(c.conversationId)}
                  className={cn(
                    'flex w-full flex-col gap-1 border-b border-border p-4 text-left hover:bg-muted/50',
                    selectedId === c.conversationId && 'bg-accent hover:bg-accent',
                  )}
                >
                  <div className="flex items-center justify-between gap-2">
                    <p className="flex items-center gap-2 truncate text-sm font-semibold">
                      {c.hasOpenHandoff && (
                        <span className="size-2 shrink-0 rounded-full bg-status-pending" title="Open handoff" />
                      )}
                      {c.customerName}
                    </p>
                    {c.lastMessageAt && (
                      <span className="shrink-0 text-xs text-muted-foreground">
                        {formatAge(c.lastMessageAt)}
                      </span>
                    )}
                  </div>
                  <p className="text-[13px] text-muted-foreground">
                    {c.messageCount} message{c.messageCount === 1 ? '' : 's'}
                  </p>
                </button>
              ))
            )}
          </div>
        </Card>

        <Card className="flex min-h-0 flex-1 flex-col overflow-hidden">
          {thread.isPending || selectedId === undefined ? (
            <p className="p-6 text-sm text-muted-foreground">
              {selectedId === undefined ? 'Select a conversation.' : 'Loading…'}
            </p>
          ) : thread.isError ? (
            <p className="p-6 text-sm text-muted-foreground">{thread.error.message}</p>
          ) : (
            <>
              <div className="border-b border-border p-6">
                <h2 className="text-lg font-bold">{thread.data.customerName}</h2>
              </div>
              <div className="flex min-h-0 flex-1 flex-col gap-3 overflow-auto p-6">
                {thread.data.messages.map((m, i) => (
                  <MessageBubble key={`${m.createdAt}-${i}`} message={m} />
                ))}
                {thread.data.messages.length === 0 && (
                  <p className="text-sm text-muted-foreground">No messages yet.</p>
                )}
              </div>
            </>
          )}
        </Card>
      </div>
    </div>
  )
}

function MessageBubble({ message }: { message: ThreadMessage }) {
  const inbound = message.direction === 'Inbound'
  return (
    <div className={cn('flex max-w-[85%] flex-col gap-1', inbound ? 'self-start' : 'self-end')}>
      <div
        className={cn(
          'rounded-2xl px-3.5 py-2 text-sm',
          inbound ? 'bg-muted' : 'bg-accent text-accent-foreground',
        )}
      >
        {message.type !== 'Text' && (
          <p className="mb-1 text-xs font-semibold opacity-70">
            {message.type === 'Audio'
              ? `Voice note${message.language ? ` · ${message.language}` : ''}`
              : message.type}
          </p>
        )}
        {message.text ?? '—'}
      </div>
      {message.toolTraces.length > 0 && (
        <div className="flex flex-col gap-1">
          {message.toolTraces.map((t, i) => (
            <details
              key={`${t.toolName}-${i}`}
              className="rounded-lg border border-border bg-background px-3 py-1.5 text-xs"
            >
              <summary className="cursor-pointer font-medium text-muted-foreground">
                {t.toolName} · {t.durationMs}ms
              </summary>
              <pre className="mt-1 overflow-auto whitespace-pre-wrap break-all text-[11px]">
                {t.argumentsJson}
                {'\n→ '}
                {t.resultJson}
              </pre>
            </details>
          ))}
        </div>
      )}
    </div>
  )
}
