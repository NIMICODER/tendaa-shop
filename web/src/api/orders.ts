import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch, type TendaaResult } from './client'

export type OrderStatus =
  | 'Draft'
  | 'AwaitingPayment'
  | 'PaymentReview'
  | 'Confirmed'
  | 'Dispatched'
  | 'Delivered'
  | 'Cancelled'

export type PaymentVerificationResult = 'Matched' | 'Mismatched' | 'Suspicious'

export interface OrderListItem {
  orderId: string
  number: number
  status: OrderStatus
  customerName: string
  itemCount: number
  grandTotal: number
  formattedGrandTotal: string
  createdAt: string
  paymentReviewAt: string | null
}

export interface OrderListPage {
  items: OrderListItem[]
  totalCount: number
  page: number
  pageSize: number
}

export interface OrderStatusCount {
  status: OrderStatus
  count: number
}

export interface OrderSummaryItem {
  productVariantId: string
  productName: string
  size: string | null
  colour: string | null
  quantity: number
  unitPrice: number
  lineTotal: number
  formattedUnitPrice: string
  formattedLineTotal: string
}

export interface PaymentProofDetail {
  paymentProofId: string
  mediaId: string
  mediaUrl: string
  extractedAmount: number | null
  formattedExtractedAmount: string | null
  senderName: string | null
  bankName: string | null
  transferDate: string | null
  reference: string | null
  confidence: number | null
  verificationResult: PaymentVerificationResult | null
  reasons: string | null
  reviewedBy: string | null
}

export interface OrderDetail {
  orderId: string
  number: number
  status: OrderStatus
  customerName: string
  customerPhone: string | null
  items: OrderSummaryItem[]
  itemTotal: number
  deliveryFee: number
  grandTotal: number
  formattedItemTotal: string
  formattedDeliveryFee: string
  formattedGrandTotal: string
  deliveryZoneName: string | null
  address: string | null
  confirmedBy: string | null
  createdAt: string
  awaitingPaymentAt: string | null
  paymentReviewAt: string | null
  confirmedAt: string | null
  paymentProofs: PaymentProofDetail[]
}

export const orderKeys = {
  all: ['orders'] as const,
  list: (status: OrderStatus | 'All', page: number) => ['orders', 'list', status, page] as const,
  counts: ['orders', 'counts'] as const,
  detail: (orderId: string | undefined) => ['orders', 'detail', orderId] as const,
}

export function useOrders(status: OrderStatus | 'All', page: number) {
  return useQuery({
    queryKey: orderKeys.list(status, page),
    queryFn: async () => {
      const params = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (status !== 'All') {
        params.set('status', status)
      }
      const result = await apiFetch<TendaaResult<OrderListPage>>(`/api/orders?${params}`)
      if (!result.isSuccessful) {
        throw new Error(result.message ?? 'Could not load orders.')
      }
      return result.data
    },
    // Polling fallback for when the SignalR connection drops; the hub invalidates
    // these same keys for immediacy when connected.
    refetchInterval: 30_000,
  })
}

export function useOrderCounts() {
  return useQuery({
    queryKey: orderKeys.counts,
    queryFn: async () => {
      const result = await apiFetch<TendaaResult<OrderStatusCount[]>>('/api/orders/counts')
      if (!result.isSuccessful) {
        throw new Error(result.message ?? 'Could not load order counts.')
      }
      return result.data
    },
    refetchInterval: 30_000,
  })
}

export function useOrderDetail(orderId: string | undefined) {
  return useQuery({
    queryKey: orderKeys.detail(orderId),
    queryFn: async () => {
      const result = await apiFetch<TendaaResult<OrderDetail>>(`/api/orders/${orderId}`)
      if (!result.isSuccessful) {
        throw new Error(result.message ?? 'Could not load this order.')
      }
      return result.data
    },
    enabled: orderId !== undefined,
  })
}

export function useConfirmPayment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (paymentProofId: string) =>
      apiFetch<TendaaResult<OrderDetail>>(`/api/orders/payment-proofs/${paymentProofId}/confirm`, {
        method: 'POST',
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: orderKeys.all })
    },
  })
}

export function useRejectPayment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ paymentProofId, reason }: { paymentProofId: string; reason: string }) =>
      apiFetch<TendaaResult<OrderDetail>>(`/api/orders/payment-proofs/${paymentProofId}/reject`, {
        method: 'POST',
        body: JSON.stringify({ reason }),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: orderKeys.all })
    },
  })
}
