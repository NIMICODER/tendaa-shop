import { useQuery } from '@tanstack/react-query'
import { apiFetch, type TendaaResult } from './client'

export type MessageDirection = 'Inbound' | 'Outbound'
export type MessageType = 'Text' | 'Audio' | 'Image' | 'System'

export interface ConversationListItem {
  conversationId: string
  customerName: string
  messageCount: number
  hasOpenHandoff: boolean
  lastMessageAt: string | null
}

export interface ThreadToolTrace {
  toolName: string
  argumentsJson: string
  resultJson: string
  durationMs: number
}

export interface ThreadMessage {
  direction: MessageDirection
  type: MessageType
  text: string | null
  language: string | null
  createdAt: string
  toolTraces: ThreadToolTrace[]
}

export interface ConversationThread {
  conversationId: string
  customerName: string
  messages: ThreadMessage[]
}

export function useConversations() {
  return useQuery({
    queryKey: ['conversations', 'list'],
    queryFn: async () => {
      const result =
        await apiFetch<TendaaResult<ConversationListItem[]>>('/api/conversations')
      if (!result.isSuccessful) {
        throw new Error(result.message ?? 'Could not load conversations.')
      }
      return result.data
    },
    refetchInterval: 30_000,
  })
}

export function useConversationThread(conversationId: string | undefined) {
  return useQuery({
    queryKey: ['conversations', 'thread', conversationId],
    queryFn: async () => {
      const result = await apiFetch<TendaaResult<ConversationThread>>(
        `/api/conversations/${conversationId}`,
      )
      if (!result.isSuccessful) {
        throw new Error(result.message ?? 'Could not load this conversation.')
      }
      return result.data
    },
    enabled: conversationId !== undefined,
  })
}
