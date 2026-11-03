import { useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { Check, ChevronLeft, ChevronRight, Clock, Receipt } from 'lucide-react'
import { useOrderCounts, useOrders, type OrderStatus } from '@/api/orders'
import { OrderStatusBadge } from '@/components/orders/badges'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { cn } from '@/lib/utils'

type TabKey = OrderStatus | 'All'

const TABS: { key: TabKey; label: string }[] = [
  { key: 'All', label: 'All' },
  { key: 'AwaitingPayment', label: 'Awaiting payment' },
  { key: 'PaymentReview', label: 'In review' },
  { key: 'Confirmed', label: 'Confirmed' },
  { key: 'Cancelled', label: 'Cancelled' },
]

const PAGE_SIZE = 20

export function OrdersPage() {
  const [tab, setTab] = useState<TabKey>('All')
  const [page, setPage] = useState(1)
  const navigate = useNavigate()
  const orders = useOrders(tab, page)
  const counts = useOrderCounts()

  function selectTab(next: TabKey) {
    setTab(next)
    setPage(1)
  }

  const countFor = (status: OrderStatus) =>
    counts.data?.find((c) => c.status === status)?.count ?? 0
  const totalPages = orders.data ? Math.max(1, Math.ceil(orders.data.totalCount / PAGE_SIZE)) : 1

  return (
    <div className="p-8">
      <h1 className="text-2xl font-bold">Orders</h1>

      <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-3">
        <StatCard
          icon={<Clock className="size-4 text-status-pending" />}
          iconBg="bg-status-pending-bg"
          label="Awaiting payment"
          value={countFor('AwaitingPayment')}
        />
        <StatCard
          icon={<Receipt className="size-4 text-primary" />}
          iconBg="bg-accent"
          label="In payment review"
          value={countFor('PaymentReview')}
        />
        <StatCard
          icon={<Check className="size-4 text-status-positive" />}
          iconBg="bg-status-positive-bg"
          label="Confirmed"
          value={countFor('Confirmed')}
        />
      </div>

      <div className="mt-6 flex flex-wrap gap-2">
        {TABS.map((t) => (
          <button
            key={t.key}
            type="button"
            onClick={() => selectTab(t.key)}
            className={cn(
              'rounded-full px-4 py-1.5 text-sm font-medium text-muted-foreground hover:bg-muted',
              tab === t.key && 'bg-accent font-semibold text-accent-foreground hover:bg-accent',
            )}
          >
            {t.label}
            {t.key !== 'All' && (
              <span className="ml-1.5 text-[13px] opacity-70">{countFor(t.key)}</span>
            )}
          </button>
        ))}
      </div>

      <Card className="mt-4 overflow-hidden">
        {orders.isPending ? (
          <p className="p-8 text-sm text-muted-foreground">Loading orders…</p>
        ) : orders.isError ? (
          <div className="flex flex-col items-start gap-3 p-8">
            <p className="text-sm text-muted-foreground">
              Couldn&apos;t load orders: {orders.error.message}
            </p>
            <Button variant="outline" size="sm" onClick={() => void orders.refetch()}>
              Retry
            </Button>
          </div>
        ) : orders.data.items.length === 0 ? (
          <p className="p-8 text-sm text-muted-foreground">No orders here yet.</p>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Order</TableHead>
                <TableHead>Customer</TableHead>
                <TableHead>Items</TableHead>
                <TableHead>Total</TableHead>
                <TableHead>Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {orders.data.items.map((order) => (
                <TableRow
                  key={order.orderId}
                  className="cursor-pointer hover:bg-muted/50"
                  onClick={() => navigate(`/orders/${order.orderId}`)}
                >
                  <TableCell className="font-semibold">#{order.number}</TableCell>
                  <TableCell>{order.customerName}</TableCell>
                  <TableCell>{order.itemCount}</TableCell>
                  <TableCell className="font-medium">{order.formattedGrandTotal}</TableCell>
                  <TableCell>
                    <OrderStatusBadge
                      status={order.status}
                      paymentReviewAt={order.paymentReviewAt}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Card>

      <div className="mt-4 flex items-center justify-between">
        <p className="text-sm text-muted-foreground">
          Page {page} of {totalPages}
          {orders.data ? ` · ${orders.data.totalCount} orders` : ''}
        </p>
        <div className="flex gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1}
            onClick={() => setPage((p) => p - 1)}
          >
            <ChevronLeft /> Prev
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => p + 1)}
          >
            Next <ChevronRight />
          </Button>
        </div>
      </div>
    </div>
  )
}

function StatCard({
  icon,
  iconBg,
  label,
  value,
}: {
  icon: ReactNode
  iconBg: string
  label: string
  value: number
}) {
  return (
    <Card>
      <CardContent className="flex items-center gap-3 p-5">
        <div className={cn('flex h-9 w-9 items-center justify-center rounded-lg', iconBg)}>
          {icon}
        </div>
        <div>
          <p className="text-[13px] font-medium text-muted-foreground">{label}</p>
          <p className="text-2xl font-bold">{value}</p>
        </div>
      </CardContent>
    </Card>
  )
}
