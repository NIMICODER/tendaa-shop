// Minimal typed HTTP layer for the dashboard API. Auth endpoints are hand-written here
// until the first Orval generation (step 3) replaces them with generated hooks — the
// antiforgery + cookie handling in apiFetch stays regardless, as Orval's mutator will reuse it.

export interface TendaaResponse {
  isSuccessful: boolean
  statusCode: number
  message: string | null
}

export interface TendaaResult<T> extends TendaaResponse {
  data: T
}

export class UnauthorizedError extends Error {
  constructor() {
    super('Not signed in.')
    this.name = 'UnauthorizedError'
  }
}

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

let antiforgeryToken: string | null = null

export function resetAntiforgeryToken() {
  antiforgeryToken = null
}

export async function fetchAntiforgeryToken(): Promise<string> {
  const response = await fetch('/api/auth/antiforgery', { credentials: 'same-origin' })
  if (!response.ok) {
    throw new ApiError(response.status, 'Could not start a secure session.')
  }
  const body = (await response.json()) as { token: string }
  antiforgeryToken = body.token
  return body.token
}

const SAFE_METHODS = ['GET', 'HEAD', 'OPTIONS']

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const method = init?.method ?? 'GET'
  const needsToken = !SAFE_METHODS.includes(method.toUpperCase())
  if (needsToken && antiforgeryToken === null) {
    await fetchAntiforgeryToken()
  }

  const response = await fetch(path, {
    ...init,
    credentials: 'same-origin',
    headers: {
      'Content-Type': 'application/json',
      ...(needsToken && antiforgeryToken !== null
        ? { RequestVerificationToken: antiforgeryToken }
        : {}),
      ...init?.headers,
    },
  })

  if (response.status === 401) {
    throw new UnauthorizedError()
  }
  if (!response.ok) {
    const message = await readErrorMessage(response)
    throw new ApiError(response.status, message)
  }
  return (await response.json()) as T
}

async function readErrorMessage(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as Partial<TendaaResponse>
    if (typeof body.message === 'string' && body.message.length > 0) {
      return body.message
    }
  } catch {
    // Not JSON — fall through to the generic message.
  }
  return `Request failed (${response.status}).`
}
