import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuthMe } from '@/api/auth'
import { UnauthorizedError } from '@/api/client'
import { Button } from '@/components/ui/button'

export function RequireAuth() {
  const { data, isPending, isError, error } = useAuthMe()
  const location = useLocation()

  if (isPending) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-muted/40">
        <p className="text-sm font-medium text-muted-foreground">Loading…</p>
      </div>
    )
  }

  if (isError && error instanceof UnauthorizedError) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  if (isError || !data) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-muted/40 px-4 text-center">
        <p className="text-sm font-medium text-muted-foreground">
          Couldn&apos;t reach the server. Check that the backend is running and try again.
        </p>
        <Button variant="outline" onClick={() => window.location.reload()}>
          Retry
        </Button>
      </div>
    )
  }

  return <Outlet />
}
