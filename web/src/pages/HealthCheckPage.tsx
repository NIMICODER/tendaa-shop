import { useQuery } from '@tanstack/react-query'

interface HealthResponse {
  status: string
  timeUtc: string
}

async function fetchHealth(): Promise<HealthResponse> {
  const response = await fetch('/api/health')
  if (!response.ok) {
    throw new Error(`Health check failed: ${response.status}`)
  }
  return (await response.json()) as HealthResponse
}

export function HealthCheckPage() {
  const { data, error, isPending } = useQuery({
    queryKey: ['health'],
    queryFn: fetchHealth,
  })

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-950 text-slate-100">
      <div className="rounded-lg border border-slate-800 bg-slate-900 px-8 py-6 text-center">
        <h1 className="text-xl font-semibold">Tendaa</h1>
        <p className="mt-2 text-sm text-slate-400">GET /api/health</p>
        {isPending && <p className="mt-4">Checking…</p>}
        {error && <p className="mt-4 text-red-400">{error.message}</p>}
        {data && (
          <p className="mt-4 font-mono text-emerald-400">
            {data.status} · {data.timeUtc}
          </p>
        )}
      </div>
    </main>
  )
}
