import type { AuthResponse, HomeData } from './types'

const base = import.meta.env.VITE_API_URL ?? 'http://localhost:8080'
let token = sessionStorage.getItem('accessToken')

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${base}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}), ...init?.headers }
  })
  if (!response.ok) {
    const detail = await response.json().catch(() => null)
    throw new Error(detail?.detail ?? detail?.title ?? 'The request could not be completed.')
  }
  return response.status === 204 ? undefined as T : response.json()
}

export const api = {
  home: () => request<HomeData>('/api/catalog/home'),
  login: (email: string, password: string) =>
    request<AuthResponse>('/api/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }),
  register: (email: string, password: string, displayName: string) =>
    request<AuthResponse>('/api/auth/register', { method: 'POST', body: JSON.stringify({ email, password, displayName }) }),
  setToken: (value: string) => { token = value; sessionStorage.setItem('accessToken', value) },
  view: (id: string) => request<void>(`/api/catalog/media/${id}/view`, { method: 'POST' }),
  checkout: (successUrl: string, cancelUrl: string) =>
    request<{ url: string }>('/api/subscriptions/checkout', { method: 'POST', body: JSON.stringify({ successUrl, cancelUrl }) })
}

