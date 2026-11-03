import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'
import {
  useConfirmPayment,
  useOrderDetail,
  useRejectPayment,
  type PaymentProofDetail,
} from '@/api/orders'
import { OrderStatusBadge, VerificationBadge } from '@/components/orders/badges'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Textarea } from '@/components/ui/textarea'

export function OrderDetailPage() {
  const { orderId } = useParams()
  const detail = useOrderDetail(orderId)
  const confirm = useConfirmPayment()
  const reject = useRejectPayment()
  const [rejectingProofId, setRejectingProofId] = useState<string | null>(null)
  const [reason, setReason] = useState('')

  if (detail.isPending) {
    return <p className="p-8 text-sm text-muted-foreground">Loading order…</p>
  }

  if (detail.isError) {
    return (
      <div className="flex flex-col items-start gap-3 p-8">
        <p className="text-sm text-muted-foreground">
          Couldn&apos;t load this order: {detail.error.message}
        </p>
        <Button variant="outline" size="sm" onClick={() => void detail.refetch()}>
          Retry
        </Button>
      </div>
    )
  }

  const order = detail.data
  const reviewable = order.status === 'PaymentReview'
  const banner = confirm.data?.message ?? reject.data?.message ?? null
  const actionError = confirm.error?.message ?? reject.error?.message ?? null

  function submitReject(proofId: string) {
    if (reason.trim().length === 0 || reject.isPending) {
      return
    }
    reject.mutate(
      { paymentProofId: proofId, reason: reason.trim() },
      {
        onSuccess: () => {
          setRejectingProofId(null)
          setReason('')
        },
      },
    )
  }

  return (
    <div className="p-8">
      <Link
        to="/orders"
        className="inline-flex items-center gap-1.5 text-sm font-medium text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="size-4" /> Orders
      </Link>

      <div className="mt-3 flex flex-wrap items-center gap-3">
        <h1 className="text-2xl font-bold">Order #{order.number}</h1>
        <OrderStatusBadge status={order.status} paymentReviewAt={order.paymentReviewAt} />
      </div>

      {banner && (
        <div className="mt-4 rounded-xl border border-border bg-muted/60 px-4 py-3 text-sm font-medium">
          {banner}
        </div>
      )}
      {actionError && (
        <div className="mt-4 rounded-xl border border-status-negative/30 bg-status-negative-bg px-4 py-3 text-sm font-medium text-status-negative">
          {actionError}
        </div>
      )}

      <div className="mt-6 grid grid-cols-1 gap-4 lg:grid-cols-3">
        <div className="flex flex-col gap-4 lg:col-span-2">
          <Card>
            <CardHeader>
              <CardTitle>Items</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-2.5">
              {order.items.map((item) => (
                <div key={item.productVariantId} className="flex items-baseline justify-between gap-4">
                  <p className="text-sm">
                    <span className="font-semibold">{item.quantity} × </span>
                    {item.productName}
                    {[item.size, item.colour].filter(Boolean).length > 0 && (
                      <span className="text-muted-foreground">
                        {' '}
                        ({[item.size, item.colour].filter(Boolean).join(' · ')})
                      </span>
                    )}
                  </p>
                  <p className="shrink-0 text-sm font-medium">{item.formattedLineTotal}</p>
                </div>
              ))}
              <div className="mt-2 flex flex-col gap-1 border-t border-border pt-3 text-sm">
                <TotalRow label="Items" value={order.formattedItemTotal} />
                <TotalRow label="Delivery" value={order.formattedDeliveryFee} />
                <TotalRow label="Total" value={order.formattedGrandTotal} bold />
              </div>
            </CardContent>
          </Card>

          {order.paymentProofs.map((proof) => (
            <ProofCard
              key={proof.paymentProofId}
              proof={proof}
              expectedTotal={order.formattedGrandTotal}
              reviewable={reviewable}
              confirming={confirm.isPending}
              rejecting={reject.isPending}
              rejectingOpen={rejectingProofId === proof.paymentProofId}
              reason={reason}
              onReasonChange={setReason}
              onConfirm={() => confirm.mutate(proof.paymentProofId)}
              onRejectOpen={() => {
                setRejectingProofId(proof.paymentProofId)
                setReason('')
              }}
              onRejectCancel={() => {
                setRejectingProofId(null)
                setReason('')
              }}
              onRejectSubmit={() => submitReject(proof.paymentProofId)}
            />
          ))}
          {order.paymentProofs.length === 0 && (
            <Card>
              <CardContent className="p-6 text-sm text-muted-foreground">
                No payment receipts yet.
              </CardContent>
            </Card>
          )}
        </div>

        <div className="flex flex-col gap-4">
          <Card>
            <CardHeader>
              <CardTitle>Customer</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-1.5 text-sm">
              <p className="font-semibold">{order.customerName}</p>
              <p className="text-muted-foreground">{order.customerPhone ?? 'Phone hidden'}</p>
              {order.address && <p className="text-muted-foreground">{order.address}</p>}
              {order.deliveryZoneName && (
                <p className="text-muted-foreground">{order.deliveryZoneName}</p>
              )}
            </CardContent>
          </Card>
          {order.confirmedBy && (
            <Card>
              <CardContent className="p-6 text-sm text-muted-foreground">
                Confirmed by <span className="font-semibold text-foreground">{order.confirmedBy}</span>
              </CardContent>
            </Card>
          )}
        </div>
      </div>
    </div>
  )
}

function TotalRow({ label, value, bold }: { label: string; value: string; bold?: boolean }) {
  return (
    <div className="flex items-baseline justify-between gap-4">
      <p className="text-muted-foreground">{label}</p>
      <p className={bold ? 'text-base font-bold' : 'font-medium'}>{value}</p>
    </div>
  )
}

function ProofCard({
  proof,
  expectedTotal,
  reviewable,
  confirming,
  rejecting,
  rejectingOpen,
  reason,
  onReasonChange,
  onConfirm,
  onRejectOpen,
  onRejectCancel,
  onRejectSubmit,
}: {
  proof: PaymentProofDetail
  expectedTotal: string
  reviewable: boolean
  confirming: boolean
  rejecting: boolean
  rejectingOpen: boolean
  reason: string
  onReasonChange: (value: string) => void
  onConfirm: () => void
  onRejectOpen: () => void
  onRejectCancel: () => void
  onRejectSubmit: () => void
}) {
  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between gap-3">
          <CardTitle>Payment receipt</CardTitle>
          <VerificationBadge result={proof.verificationResult} />
        </div>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <img
          src={proof.mediaUrl}
          alt="Payment receipt"
          className="max-h-96 w-full rounded-xl border border-border object-contain bg-muted/40"
        />

        <dl className="grid grid-cols-2 gap-x-6 gap-y-2 text-sm">
          <Field label="Extracted amount" value={proof.formattedExtractedAmount} />
          <Field label="Expected total" value={expectedTotal} />
          <Field label="Sender" value={proof.senderName} />
          <Field label="Bank" value={proof.bankName} />
          <Field label="Transfer date" value={proof.transferDate} />
          <Field label="Reference" value={proof.reference} mono />
          <Field
            label="Confidence"
            value={proof.confidence === null ? null : `${Math.round(proof.confidence * 100)}%`}
          />
          <Field label="Reviewed by" value={proof.reviewedBy} />
        </dl>

        {proof.reasons && <p className="text-sm text-muted-foreground">{proof.reasons}</p>}

        {reviewable && !rejectingOpen && (
          <div className="flex gap-2">
            <Button onClick={onConfirm} disabled={confirming || rejecting}>
              {confirming ? 'Confirming…' : 'Confirm payment'}
            </Button>
            <Button variant="outline" onClick={onRejectOpen} disabled={confirming || rejecting}>
              Reject
            </Button>
          </div>
        )}

        {reviewable && rejectingOpen && (
          <div className="flex flex-col gap-2">
            <Textarea
              placeholder="Why is this receipt wrong? The customer will see this."
              value={reason}
              onChange={(event) => onReasonChange(event.target.value)}
              autoFocus
            />
            <div className="flex gap-2">
              <Button
                variant="destructive"
                onClick={onRejectSubmit}
                disabled={rejecting || reason.trim().length === 0}
              >
                {rejecting ? 'Rejecting…' : 'Reject payment'}
              </Button>
              <Button variant="ghost" onClick={onRejectCancel} disabled={rejecting}>
                Cancel
              </Button>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  )
}

function Field({
  label,
  value,
  mono,
}: {
  label: string
  value: string | null
  mono?: boolean
}) {
  return (
    <div className="flex flex-col">
      <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
      <dd className={mono ? 'font-mono text-[13px]' : 'font-medium'}>{value ?? '—'}</dd>
    </div>
  )
}
