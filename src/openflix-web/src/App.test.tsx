import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import App from './App'

vi.stubGlobal('fetch', vi.fn(() => Promise.reject(new Error('offline'))))

describe('OpenFlix', () => {
  it('renders the fallback experience when the API is unavailable', async () => {
    render(<App />)
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(await screen.findByRole('heading', { name: 'Night of the Living Dead' })).toBeInTheDocument()
  })
})
