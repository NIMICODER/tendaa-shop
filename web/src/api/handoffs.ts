import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch, type TendaaResult } from './client'

export type HandoffStatus = 'Open' | 'Resolved'

export interface HandoffListItem {
  handoffId: string
  conversationId: string
  customerName: string
  reason: string
  summary: string
  status: HandoffStatus
  createdAt: string
  resolvedAt: string | null
  resolvedBy: string | null
}

export interface HandoffMessage {
  direction: 'Inbound' | 'Outbound'
  text: string | null
  createdAt: string
}

export interface HandoffDetail extends HandoffListItem {
  customerPhone: string | null
  recentMessages: HandoffMessage[]
}

export const handoffKeys = {
  all: ['handoffs'] as const,
  list: (status: HandoffStatus | 'All') => ['handoffs', 'list', status] as const,
  detail: (handoffId: string | undefined) => ['handoffs', 'detail', handoffId] as const,
}

export function useHandoffs(status: HandoffStatus | 'All') {
  return useQuery({
    queryKey: handoffKeys.list(status),
    queryFn: async () => {
      const params = status === 'All' ? '' : `?status=${status}`
      const result = await apiFetch<TendaaResult<HandoffListItem[]>>(`/api/handoffs${params}`)
      if (!result.isSuccessful) {
        throw new Error(result.message ?? 'Could not load handoffs.')
      }
      return result.data
    },
    refetchInterval: 30_000,
  })
}

export function useHandoffDetail(handoffId: string | undefined) {
  return useQuery({
    queryKey: handoffKeys.detail(handoffId),
    queryFn: async () => {
      const result = await apiFetch<TendaaResult<HandoffDetail>>(`/api/handoffs/${handoffId}`)
      if (!result.isSuccessful) {
        throw new Error(result.message ?? 'Could not load this handoff.')
      }
      return result.data
    },
    enabled: handoffId !== undefined,
  })
}

export function useResolveHandoff() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ handoffId, replyText }: { handoffId: string; replyText: string }) =>
      apiFetch<TendaaResult<HandoffDetail>>(`/api/handoffs/${handoffId}/resolve`, {
        method: 'POST',
        body: JSON.stringify({ replyText }),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: handoffKeys.all })
    },
  })
}
