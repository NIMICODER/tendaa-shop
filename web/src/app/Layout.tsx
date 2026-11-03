import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import {
  Inbox,
  LogOut,
  MessagesSquare,
  Package,
  Settings,
  ShoppingBag,
} from 'lucide-react'
import { useLogout } from '@/api/auth'
import { SignalRProvider } from '@/signalr/SignalRProvider'
import { cn } from '@/lib/utils'

const NAV_ITEMS = [
  { to: '/orders', label: 'Orders', icon: ShoppingBag },
  { to: '/handoffs', label: 'Handoffs', icon: Inbox },
  { to: '/conversations', label: 'Conversations', icon: MessagesSquare },
  { to: '/catalog', label: 'Catalog', icon: Package },
  { to: '/settings', label: 'Settings', icon: Settings },
]

export function Layout() {
  const navigate = useNavigate()
  const logout = useLogout()

  function onLogout() {
    logout.mutate(undefined, { onSuccess: () => navigate('/login', { replace: true }) })
  }

  return (
    <div className="flex min-h-screen bg-muted/40">
      <aside className="flex w-64 shrink-0 flex-col border-r border-sidebar-border bg-sidebar px-4 py-5">
        <div className="flex items-center gap-2.5 px-2">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-sm font-bold text-primary-foreground shadow-sm">
            T
          </div>
          <span className="text-lg font-bold text-sidebar-foreground">Tendaa</span>
        </div>

        <nav className="mt-8 flex flex-1 flex-col gap-1">
          {NAV_ITEMS.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-3 rounded-xl px-3.5 py-2.5 text-sm font-medium text-sidebar-muted-foreground transition-colors hover:bg-muted',
                  isActive &&
                    'bg-sidebar-accent font-semibold text-sidebar-accent-foreground hover:bg-sidebar-accent',
                )
              }
            >
              <item.icon className="size-[18px]" />
              {item.label}
            </NavLink>
          ))}
        </nav>

        <div className="border-t border-sidebar-border pt-4">
          <div className="flex items-center gap-3 px-2">
            <div className="flex h-9 w-9 items-center justify-center rounded-full bg-primary text-sm font-bold text-primary-foreground">
              S
            </div>
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-semibold text-sidebar-foreground">Seller</p>
              <p className="truncate text-xs text-sidebar-muted-foreground">Admin</p>
            </div>
            <button
              type="button"
              onClick={onLogout}
              disabled={logout.isPending}
              title="Sign out"
              className="flex h-9 w-9 items-center justify-center rounded-lg text-sidebar-muted-foreground transition-colors hover:bg-muted hover:text-foreground disabled:opacity-50"
            >
              <LogOut className="size-[18px]" />
            </button>
          </div>
        </div>
      </aside>

      <main className="min-w-0 flex-1">
        <SignalRProvider>
          <Outlet />
        </SignalRProvider>
      </main>
    </div>
  )
}
