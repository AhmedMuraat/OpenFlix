import type { AuthResponse, HomeData, Plan, Subscription } from './types'

const configuredBase = import.meta.env.VITE_API_URL?.replace(/\/$/, '')
const base = configuredBase || (import.meta.env.DEV ? 'http://localhost:8080' : '')
let token = sessionStorage.getItem('accessToken')
let refreshPromise: Promise<AuthResponse | null> | null = null

function remember(response: AuthResponse) {
  token = response.accessToken
  sessionStorage.setItem('accessToken', response.accessToken)
  return response
}

function forget() {
  token = null
  sessionStorage.removeItem('accessToken')
}

async function errorFrom(response: Response) {
  const detail = await response.json().catch(() => null)
  return new Error(detail?.detail ?? detail?.title ?? 'The request could not be completed.')
}

async function restoreSession(): Promise<AuthResponse | null> {
  if (refreshPromise) return refreshPromise
  refreshPromise = fetch(`${base}/api/auth/refresh`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' }
  }).then(async response => {
    if (!response.ok) {
      forget()
      return null
    }
    return remember(await response.json() as AuthResponse)
  }).catch(() => {
    forget()
    return null
  }).finally(() => {
    refreshPromise = null
  })
  return refreshPromise
}

async function request<T>(path: string, init?: RequestInit, allowRefresh = true): Promise<T> {
  const response = await fetch(`${base}${path}`, {
    ...init,
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init?.headers
    }
  })

  if (response.status === 401 && allowRefresh && !path.startsWith('/api/auth/')) {
    const restored = await restoreSession()
    if (restored) return request<T>(path, init, false)
  }
  if (!response.ok) throw await errorFrom(response)
  return response.status === 204 ? undefined as T : response.json()
}

async function authenticate(path: string, body: object) {
  const response = await request<AuthResponse>(path, {
    method: 'POST',
    body: JSON.stringify(body)
  }, false)
  return remember(response)
}

export const api = {
  home: () => request<HomeData>('/api/catalog/home'),
  login: (email: string, password: string) =>
    authenticate('/api/auth/login', { email, password }),
  register: (email: string, password: string, displayName: string) =>
    authenticate('/api/auth/register', { email, password, displayName }),
  restoreSession,
  logout: async () => {
    try {
      await request<void>('/api/auth/logout', { method: 'POST' }, false)
    } finally {
      forget()
    }
  },
  plans: () => request<Plan[]>('/api/subscriptions/plans'),
  subscription: () => request<Subscription | null>('/api/subscriptions/me'),
  view: (id: string) => request<void>(`/api/catalog/media/${id}/view`, { method: 'POST' }),
  checkout: (successUrl: string, cancelUrl: string) =>
    request<{ url: string }>('/api/subscriptions/checkout', {
      method: 'POST',
      body: JSON.stringify({ successUrl, cancelUrl })
    })
}
