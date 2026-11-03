import { Navigate, Link } from 'react-router-dom'
import { Mic, Image as ImageIcon, ShieldCheck, Users, LayoutDashboard, ArrowRight } from 'lucide-react'
import { useAuthMe } from '@/api/auth'
import { buttonVariants } from '@/components/ui/button'
import { cn } from '@/lib/utils'

const VALUE_PROPS = [
  {
    icon: Mic,
    title: 'Takes orders from anything',
    body: 'Text, voice notes, or a photo of what they want — the agent pulls out the items, size, quantity and delivery area, even from a messy message.',
  },
  {
    icon: ImageIcon,
    title: 'Quotes it and asks for payment',
    body: 'Item total plus delivery by zone, sent with your bank details and an order number — no back-and-forth to get a price out.',
  },
  {
    icon: ShieldCheck,
    title: 'Checks the receipt before you do',
    body: "Reads the amount, bank, date and reference off the transfer screenshot and compares it with the order. It never confirms a payment on its own — that's always your call.",
  },
  {
    icon: Users,
    title: 'Knows when to hand off',
    body: "Complaints, refunds, or anything it isn't sure about go straight to you with a short summary — it stops answering until you've handled it.",
  },
]

export function LandingPage() {
  const authMe = useAuthMe()
  if (authMe.data) {
    return <Navigate to="/orders" replace />
  }

  return (
    <div className="min-h-screen bg-background">
      <header className="mx-auto flex max-w-6xl items-center justify-between px-6 py-6">
        <div className="flex items-center gap-2.5">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-sm font-bold text-primary-foreground">
            T
          </div>
          <span className="text-lg font-bold text-foreground">Tendaa</span>
        </div>
        <Link to="/login" className={cn(buttonVariants({ variant: 'outline', size: 'sm' }))}>
          Seller log in
        </Link>
      </header>

      <main>
        <section className="mx-auto max-w-4xl px-6 pt-16 pb-20 text-center">
          <span className="inline-flex items-center rounded-full bg-accent px-3 py-1 text-xs font-semibold text-accent-foreground">
            A WhatsApp sales agent for African sellers
          </span>
          <h1 className="mt-6 text-4xl font-bold tracking-tight text-foreground sm:text-5xl">
            Your WhatsApp sales floor, organized.
          </h1>
          <p className="mx-auto mt-5 max-w-2xl text-lg text-muted-foreground">
            Tendaa answers your customers, takes their order, quotes the total with delivery, and
            checks their payment receipt — in English or Pidgin, from text, voice notes, or
            photos. You confirm the money and handle what needs a person.
          </p>
          <div className="mt-8 flex items-center justify-center gap-3">
            <Link to="/login" className={cn(buttonVariants({ size: 'lg' }))}>
              Go to your dashboard
              <ArrowRight />
            </Link>
          </div>
        </section>

        <section className="border-y border-border bg-muted/40 py-16">
          <div className="mx-auto max-w-6xl px-6">
            <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
              {VALUE_PROPS.map((item) => (
                <div
                  key={item.title}
                  className="rounded-2xl border border-border bg-card p-6 shadow-sm"
                >
                  <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-accent text-accent-foreground">
                    <item.icon className="size-5" />
                  </div>
                  <h3 className="mt-4 text-sm font-semibold text-foreground">{item.title}</h3>
                  <p className="mt-2 text-sm text-muted-foreground">{item.body}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        <section className="mx-auto max-w-4xl px-6 py-20 text-center">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-accent text-accent-foreground">
            <LayoutDashboard className="size-6" />
          </div>
          <h2 className="mt-5 text-2xl font-bold text-foreground">
            One dashboard for the part that needs you.
          </h2>
          <p className="mx-auto mt-3 max-w-xl text-muted-foreground">
            Confirm or reject a payment with the receipt right beside it, reply to a handoff, and
            manage your catalog — all backed by the same rules the agent uses on WhatsApp.
          </p>
          <Link to="/login" className={cn(buttonVariants({ size: 'lg' }), 'mt-7')}>
            Seller log in
            <ArrowRight />
          </Link>
        </section>
      </main>

      <footer className="border-t border-border py-8 text-center text-sm text-muted-foreground">
        Tendaa — built for sellers in Lagos, and anywhere else WhatsApp is the shop front.
      </footer>
    </div>
  )
}
