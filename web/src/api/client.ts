const TOKEN_KEY = 'openiron.token'

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

export function getToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY)
  } catch {
    return null
  }
}

export function setToken(token: string | null) {
  try {
    if (token) localStorage.setItem(TOKEN_KEY, token)
    else localStorage.removeItem(TOKEN_KEY)
  } catch {
    // Storage can be unavailable (private windows); the session then lasts until the page closes.
  }
}

let onUnauthorized: () => void = () => {}

/** Called when a request made with a token is rejected, so the app can drop the session. */
export function setUnauthorizedHandler(handler: () => void) {
  onUnauthorized = handler
}

interface ProblemBody {
  title?: string
  errors?: Record<string, string[]>
}

async function errorMessage(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as ProblemBody
    const first = body.errors && Object.values(body.errors)[0]?.[0]
    return first ?? body.title ?? response.statusText
  } catch {
    return response.statusText || `Request failed (${response.status})`
  }
}

export async function api<T>(path: string, init: { method?: string; body?: unknown } = {}): Promise<T> {
  const token = getToken()
  const response = await fetch(`/api${path}`, {
    method: init.method ?? 'GET',
    headers: {
      ...(init.body !== undefined && { 'Content-Type': 'application/json' }),
      ...(token && { Authorization: `Bearer ${token}` }),
    },
    body: init.body !== undefined ? JSON.stringify(init.body) : undefined,
  })

  if (response.status === 401 && token) onUnauthorized()
  if (!response.ok) throw new ApiError(response.status, await errorMessage(response))
  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}
