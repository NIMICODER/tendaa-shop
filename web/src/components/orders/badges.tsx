import { Badge } from '@/components/ui/badge'
import { formatAge } from '@/lib/format'
import type { OrderStatus, PaymentVerificationResult } from '@/api/orders'

const STATUS_LABELS: Record<OrderStatus, string> = {
  Draft: 'Draft',
  AwaitingPayment: 'Awaiting payment',
  PaymentReview: 'In review',
  Confirmed: 'Confirmed',
  Dispatched: 'Dispatched',
  Delivered: 'Delivered',
  Cancelled: 'Cancelled',
}

const STATUS_VARIANTS: Record<OrderStatus, 'default' | 'success' | 'warning' | 'danger' | 'muted'> =
  {
    Draft: 'muted',
    AwaitingPayment: 'warning',
    PaymentReview: 'default',
    Confirmed: 'success',
    Dispatched: 'success',
    Delivered: 'success',
    Cancelled: 'danger',
  }

export function OrderStatusBadge({
  status,
  paymentReviewAt,
}: {
  status: OrderStatus
  paymentReviewAt?: string | null
}) {
  return (
    <span className="inline-flex items-center gap-2">
      <Badge variant={STATUS_VARIANTS[status]}>{STATUS_LABELS[status]}</Badge>
      {status === 'PaymentReview' && paymentReviewAt && (
        <span className="text-xs whitespace-nowrap text-muted-foreground">
          {formatAge(paymentReviewAt)} waiting
        </span>
      )}
    </span>
  )
}

const VERIFICATION_VARIANTS = {
  Matched: 'success',
  Mismatched: 'danger',
  Suspicious: 'warning',
} as const

export function VerificationBadge({ result }: { result: PaymentVerificationResult | null }) {
  if (!result) {
    return <Badge variant="muted">Not checked</Badge>
  }
  return <Badge variant={VERIFICATION_VARIANTS[result]}>{result}</Badge>
}
