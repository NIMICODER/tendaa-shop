import { useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuthMe, useLogin } from '@/api/auth'
import { UnauthorizedError } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

const VALUE_LINES = [
  'Takes orders from text, voice notes and photos',
  'Checks the receipt before telling the customer anything',
  'Hands off anything unusual, straight to you',
]

export function LoginPage() {
  const [password, setPassword] = useState('')
  const navigate = useNavigate()
  const location = useLocation()
  const from = (location.state as { from?: string } | null)?.from ?? '/orders'

  const alreadySignedIn = useAuthMe()
  const login = useLogin()

  if (alreadySignedIn.data) {
    return <Navigate to="/orders" replace />
  }

  const errorMessage =
    login.error instanceof UnauthorizedError
      ? 'Invalid password.'
      : (login.error?.message ?? null)

  function onSubmit(event: FormEvent) {
    event.preventDefault()
    if (password.length === 0 || login.isPending) {
      return
    }
    login.mutate(password, { onSuccess: () => navigate(from, { replace: true }) })
  }

  return (
    <div className="flex min-h-screen">
      {/* Brand panel — hidden on small screens, the form alone is enough there. */}
      <div className="relative hidden w-1/2 overflow-hidden bg-[#111014] lg:flex lg:flex-col lg:justify-between lg:p-10">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -top-24 -left-24 h-96 w-96 rounded-full bg-primary/40 blur-3xl"
        />
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -right-20 bottom-0 h-80 w-80 rounded-full bg-accent/30 blur-3xl"
        />

        <Link to="/" className="relative flex items-center gap-2.5">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-sm font-bold text-white">
            T
          </div>
          <span className="text-lg font-bold text-white">Tendaa</span>
        </Link>

        <div className="relative">
          <h1 className="max-w-sm text-3xl font-bold leading-tight text-white">
            Your WhatsApp sales floor, organized.
          </h1>
          <ul className="mt-6 flex flex-col gap-2.5">
            {VALUE_LINES.map((line) => (
              <li key={line} className="flex items-start gap-2.5 text-sm text-white/70">
                <span className="mt-1.5 size-1.5 shrink-0 rounded-full bg-primary" />
                {line}
              </li>
            ))}
          </ul>
        </div>
      </div>

      {/* Form panel */}
      <div className="flex w-full flex-col items-center justify-center bg-background px-6 py-12 lg:w-1/2">
        <div className="w-full max-w-sm">
          <div className="mb-8 flex items-center gap-2.5 lg:hidden">
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-sm font-bold text-primary-foreground">
              T
            </div>
            <span className="text-lg font-bold text-foreground">Tendaa</span>
          </div>

          <h2 className="text-2xl font-bold text-foreground">Welcome back</h2>
          <p className="mt-1.5 text-sm text-muted-foreground">
            Sign in with your dashboard password to continue.
          </p>

          <form onSubmit={onSubmit} className="mt-7 flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <label htmlFor="password" className="text-sm font-medium text-foreground">
                Admin password
              </label>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                autoFocus
                value={password}
                onChange={(event) => setPassword(event.target.value)}
              />
            </div>
            {errorMessage !== null && (
              <p className="text-sm font-medium text-destructive">{errorMessage}</p>
            )}
            <Button
              type="submit"
              size="lg"
              className="mt-1 w-full"
              disabled={login.isPending || password.length === 0}
            >
              {login.isPending ? 'Signing in…' : 'Log in'}
            </Button>
          </form>
        </div>
      </div>
    </div>
  )
}
