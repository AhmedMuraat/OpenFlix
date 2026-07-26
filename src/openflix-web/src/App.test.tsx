import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

vi.stubGlobal('fetch', vi.fn(() => Promise.reject(new Error('offline'))))

describe('OpenFlix', () => {
  beforeEach(() => {
    localStorage.clear()
    window.scrollTo = vi.fn()
  })
  afterEach(cleanup)

  it('renders the fallback experience when the API is unavailable', async () => {
    render(<App />)
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(await screen.findByRole('heading', { name: 'Night of the Living Dead' })).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: 'More information about His Girl Friday' }).length).toBeGreaterThan(0)
  })

  it('searches across title, year, synopsis, and genre', async () => {
    render(<App />)
    fireEvent.change(screen.getByRole('textbox', { name: 'Search the catalog' }), { target: { value: '1926' } })
    expect(await screen.findByRole('heading', { name: 'Results for “1926”' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'More information about The General' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'More information about Charade' })).not.toBeInTheDocument()
  })

  it('persists saved films in My List', async () => {
    render(<App />)
    const saveButtons = await screen.findAllByRole('button', { name: 'Add His Girl Friday to My List' })
    fireEvent.click(saveButtons[0])
    fireEvent.click(screen.getByRole('button', { name: 'My List' }))
    expect(screen.getByRole('heading', { name: 'My List' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'More information about His Girl Friday' })).toBeInTheDocument()
  })
})
