import { useEffect, useMemo, useState } from 'react'
import {
  ArrowLeft, Bookmark, Check, ChevronDown, CircleAlert, Clock3, ExternalLink,
  Film, Heart, Info, Menu, Play, Search, ShieldCheck, Sparkles, X
} from 'lucide-react'
import { api } from './api'
import { fallbackCatalog, fallbackPlans } from './catalog'
import type { AuthResponse, HomeData, Media, Plan, Subscription } from './types'

type Section = 'home' | 'movies' | 'series' | 'my-list' | 'pricing'

function useStoredIds(key: string) {
  const [ids, setIds] = useState<string[]>(() => {
    try {
      return JSON.parse(localStorage.getItem(key) ?? '[]') as string[]
    } catch {
      return []
    }
  })

  useEffect(() => {
    localStorage.setItem(key, JSON.stringify(ids))
  }, [ids, key])

  return [ids, setIds] as const
}

function Card({
  item,
  onPlay,
  onDetails,
  saved,
  onToggleSaved
}: {
  item: Media
  onPlay: (item: Media) => void
  onDetails: (item: Media) => void
  saved: boolean
  onToggleSaved: (item: Media) => void
}) {
  return (
    <article className="card">
      <button className="card-art" onClick={() => onDetails(item)} aria-label={`More information about ${item.title}`}>
        <img src={item.posterUrl} alt={`${item.title} poster`} loading="lazy" />
        <span className="card-shade" />
        <span className="card-copy">
          <span className="card-meta">{item.year} · {item.genre}</span>
          <b>{item.title}</b>
        </span>
      </button>
      <div className="card-actions">
        <button className="card-play" onClick={() => onPlay(item)} aria-label={`Play ${item.title}`}>
          <Play fill="currentColor" size={15} />
        </button>
        <button className="card-save" onClick={() => onToggleSaved(item)} aria-label={`${saved ? 'Remove' : 'Add'} ${item.title} ${saved ? 'from' : 'to'} My List`}>
          {saved ? <Check size={16} /> : <Bookmark size={16} />}
        </button>
      </div>
    </article>
  )
}

function Row({
  title,
  eyebrow,
  items,
  onPlay,
  onDetails,
  savedIds,
  onToggleSaved,
  onExplore
}: {
  title: string
  eyebrow?: string
  items: Media[]
  onPlay: (item: Media) => void
  onDetails: (item: Media) => void
  savedIds: string[]
  onToggleSaved: (item: Media) => void
  onExplore?: () => void
}) {
  if (!items.length) return null
  return (
    <section className="row" aria-labelledby={`row-${title.replace(/\W/g, '-').toLowerCase()}`}>
      <div className="row-title">
        <div>
          {eyebrow && <span>{eyebrow}</span>}
          <h2 id={`row-${title.replace(/\W/g, '-').toLowerCase()}`}>{title}</h2>
        </div>
        {onExplore && <button onClick={onExplore}>Explore all <span>→</span></button>}
      </div>
      <div className="rail">
        {items.map(item => (
          <Card
            key={item.id}
            item={item}
            onPlay={onPlay}
            onDetails={onDetails}
            saved={savedIds.includes(item.id)}
            onToggleSaved={onToggleSaved}
          />
        ))}
      </div>
    </section>
  )
}

function BrowseView({
  title,
  copy,
  items,
  selectedGenre,
  setSelectedGenre,
  onPlay,
  onDetails,
  savedIds,
  onToggleSaved
}: {
  title: string
  copy: string
  items: Media[]
  selectedGenre: string
  setSelectedGenre: (genre: string) => void
  onPlay: (item: Media) => void
  onDetails: (item: Media) => void
  savedIds: string[]
  onToggleSaved: (item: Media) => void
}) {
  const genres = ['All', ...new Set(items.map(item => item.genre))]
  const filtered = selectedGenre === 'All' ? items : items.filter(item => item.genre === selectedGenre)

  return (
    <section className="browse-page">
      <div className="browse-heading">
        <span className="eyebrow"><Film size={14} /> THE OPEN CATALOG</span>
        <h1>{title}</h1>
        <p>{copy}</p>
      </div>
      <div className="genre-tabs" aria-label="Filter by genre">
        {genres.map(genre => (
          <button
            key={genre}
            className={selectedGenre === genre ? 'selected' : ''}
            onClick={() => setSelectedGenre(genre)}
            aria-pressed={selectedGenre === genre}
          >
            {genre}
          </button>
        ))}
      </div>
      <div className="catalog-count">{filtered.length} {filtered.length === 1 ? 'title' : 'titles'}</div>
      <div className="catalog-grid">
        {filtered.map(item => (
          <Card
            key={item.id}
            item={item}
            onPlay={onPlay}
            onDetails={onDetails}
            saved={savedIds.includes(item.id)}
            onToggleSaved={onToggleSaved}
          />
        ))}
      </div>
    </section>
  )
}

function EmptyState({ icon, title, copy, action }: { icon: React.ReactNode; title: string; copy: string; action?: React.ReactNode }) {
  return (
    <section className="empty-state">
      <span>{icon}</span>
      <h2>{title}</h2>
      <p>{copy}</p>
      {action}
    </section>
  )
}

function AuthModal({ close, success }: { close: () => void; success: (auth: AuthResponse) => void }) {
  const [register, setRegister] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setBusy(true)
    setError('')
    const data = new FormData(event.currentTarget)
    try {
      const result = register
        ? await api.register(String(data.get('email')), String(data.get('password')), String(data.get('name')))
        : await api.login(String(data.get('email')), String(data.get('password')))
      api.setToken(result.accessToken)
      success(result)
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Try again.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true" aria-labelledby="auth-title">
      <div className="auth-card">
        <button className="close" onClick={close} aria-label="Close"><X /></button>
        <span className="eyebrow">YOUR CINEMA AWAITS</span>
        <h2 id="auth-title">{register ? 'Create your account' : 'Welcome back'}</h2>
        <p>{register ? 'Build a watchlist and continue discovering film history.' : 'Continue where the story left off.'}</p>
        <form onSubmit={submit}>
          {register && <label>Display name<input name="name" required minLength={2} autoFocus /></label>}
          <label>Email<input name="email" type="email" autoComplete="email" required autoFocus={!register} /></label>
          <label>Password<input name="password" type="password" autoComplete={register ? 'new-password' : 'current-password'} required minLength={12} /></label>
          {error && <div className="error" role="alert">{error}</div>}
          <button className="primary wide" disabled={busy}>{busy ? 'Please wait…' : register ? 'Create account' : 'Sign in'}</button>
        </form>
        <button className="switch" onClick={() => setRegister(!register)}>
          {register ? 'Already a member? Sign in' : 'New here? Create an account'}
        </button>
      </div>
    </div>
  )
}

function DetailsModal({
  item,
  saved,
  close,
  play,
  toggleSaved
}: {
  item: Media
  saved: boolean
  close: () => void
  play: () => void
  toggleSaved: () => void
}) {
  return (
    <div className="modal-backdrop details-backdrop" role="dialog" aria-modal="true" aria-labelledby="details-title">
      <article className="details-card">
        <div className="details-art" style={{ backgroundImage: `linear-gradient(90deg,#0c0c10 3%,rgba(12,12,16,.72) 48%,rgba(12,12,16,.08)),url(${item.backdropUrl})` }}>
          <button className="close" onClick={close} aria-label="Close details"><X /></button>
          <div>
            <span className="eyebrow"><Sparkles size={13} /> CURATED CLASSIC</span>
            <h2 id="details-title">{item.title}</h2>
            <div className="facts">
              <b>Open access</b><span>{item.year}</span><span className="rating">{item.maturityRating}+</span><span>{item.genre}</span>
            </div>
          </div>
        </div>
        <div className="details-body">
          <div>
            <p className="details-synopsis">{item.synopsis}</p>
            <div className="details-actions">
              <button className="primary" onClick={play}><Play fill="currentColor" size={18} /> Play film</button>
              <button className="secondary" onClick={toggleSaved}>
                {saved ? <Check size={18} /> : <Bookmark size={18} />}
                {saved ? 'In My List' : 'Add to My List'}
              </button>
            </div>
          </div>
          <aside className="rights-panel">
            <span><ShieldCheck size={18} /> Rights & source</span>
            <p>{item.license}</p>
            <small>{item.attribution}</small>
            <a href={item.sourceUrl} target="_blank" rel="noreferrer">Inspect original source <ExternalLink size={14} /></a>
          </aside>
        </div>
      </article>
    </div>
  )
}

function Player({ item, close }: { item: Media; close: () => void }) {
  useEffect(() => {
    api.view(item.id).catch(() => {})
  }, [item.id])

  return (
    <div className="player-modal" role="dialog" aria-modal="true" aria-labelledby="player-title">
      <div className="player-bar">
        <button onClick={close} aria-label="Close player"><ArrowLeft /></button>
        <div><small>NOW PLAYING</small><b id="player-title">{item.title}</b></div>
      </div>
      <iframe src={item.streamUrl} title={item.title} allowFullScreen allow="autoplay; fullscreen" />
      <div className="player-meta">
        <div>
          <span className="eyebrow">{item.genre} · {item.year}</span>
          <h2>{item.title}</h2>
          <p>{item.synopsis}</p>
        </div>
        <div className="rights">
          <b><ShieldCheck size={16} /> Source & rights</b>
          <span>{item.license}</span>
          <a href={item.sourceUrl} target="_blank" rel="noreferrer">View original source <ExternalLink size={14} /></a>
        </div>
      </div>
    </div>
  )
}

function Pricing({ plans, subscription, signedIn, signIn }: {
  plans: Plan[]
  subscription: Subscription | null
  signedIn: boolean
  signIn: () => void
}) {
  const [busy, setBusy] = useState(false)
  const isSupporter = subscription?.status === 1 || subscription?.status === 2

  async function subscribe() {
    if (!signedIn) {
      signIn()
      return
    }
    setBusy(true)
    try {
      const result = await api.checkout(`${location.origin}/?checkout=success`, `${location.origin}/?checkout=cancelled`)
      location.assign(result.url)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="pricing-page">
      <span className="eyebrow">SUPPORT OPEN CINEMA</span>
      <h1>Simple plans. No locked-up culture.</h1>
      <p>Watch the open catalog free. Supporters fund careful rights review and preservation-focused features.</p>
      <div className="plans">
        {plans.map(plan => (
          <article className={plan.id === 'supporter' ? 'plan featured-plan' : 'plan'} key={plan.id}>
            {plan.id === 'supporter' && <small className="plan-label">MOST IMPACT</small>}
            <span>{plan.name}</span>
            <h2>{plan.price ? `€${plan.price}` : 'Free'}<small>{plan.price ? '/month' : ''}</small></h2>
            <ul>{plan.features.map(feature => <li key={feature}><Check size={16} /> {feature}</li>)}</ul>
            {plan.id === 'supporter'
              ? <button className="primary wide" onClick={subscribe} disabled={busy || isSupporter}>{isSupporter ? 'Active supporter' : busy ? 'Opening checkout…' : 'Become a supporter'}</button>
              : <button className="secondary wide" onClick={() => location.assign('/')}>Browse free</button>}
          </article>
        ))}
      </div>
      <small>Payments are handled by Stripe. OpenFlix never stores card details.</small>
    </section>
  )
}

export default function App() {
  const [data, setData] = useState<HomeData>(fallbackCatalog)
  const [active, setActive] = useState<Section>('home')
  const [player, setPlayer] = useState<Media | null>(null)
  const [details, setDetails] = useState<Media | null>(null)
  const [authOpen, setAuthOpen] = useState(false)
  const [mobileOpen, setMobileOpen] = useState(false)
  const [user, setUser] = useState<AuthResponse['user'] | null>(null)
  const [query, setQuery] = useState('')
  const [selectedGenre, setSelectedGenre] = useState('All')
  const [plans, setPlans] = useState<Plan[]>(fallbackPlans)
  const [subscription, setSubscription] = useState<Subscription | null>(null)
  const [catalogOnline, setCatalogOnline] = useState<boolean | null>(null)
  const [toast, setToast] = useState('')
  const [savedIds, setSavedIds] = useStoredIds('openflix-watchlist')
  const [recentIds, setRecentIds] = useStoredIds('openflix-recent')

  const allTitles = useMemo(() => {
    const byId = new Map([...data.movies, ...data.series, data.featured].map(item => [item.id, item]))
    return [...byId.values()]
  }, [data])

  useEffect(() => {
    api.home()
      .then(result => {
        setData(result)
        setCatalogOnline(true)
      })
      .catch(() => setCatalogOnline(false))
    api.plans().then(setPlans).catch(() => {})
  }, [])

  useEffect(() => {
    const modalOpen = Boolean(player || details || authOpen || mobileOpen)
    document.body.classList.toggle('modal-open', modalOpen)
    return () => document.body.classList.remove('modal-open')
  }, [player, details, authOpen, mobileOpen])

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== 'Escape') return
      if (player) setPlayer(null)
      else if (details) setDetails(null)
      else if (authOpen) setAuthOpen(false)
      else setMobileOpen(false)
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [player, details, authOpen])

  useEffect(() => {
    if (!toast) return
    const timeout = window.setTimeout(() => setToast(''), 2200)
    return () => window.clearTimeout(timeout)
  }, [toast])

  const searchResults = useMemo(() => {
    const normalized = query.trim().toLowerCase()
    if (!normalized) return []
    return allTitles.filter(item => `${item.title} ${item.genre} ${item.year} ${item.synopsis}`.toLowerCase().includes(normalized))
  }, [allTitles, query])

  const savedTitles = savedIds.map(id => allTitles.find(item => item.id === id)).filter(Boolean) as Media[]
  const recentTitles = recentIds.map(id => allTitles.find(item => item.id === id)).filter(Boolean) as Media[]

  function navigate(section: Section) {
    setActive(section)
    setQuery('')
    setSelectedGenre('All')
    setMobileOpen(false)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  function play(item: Media) {
    setDetails(null)
    setPlayer(item)
    setRecentIds(current => [item.id, ...current.filter(id => id !== item.id)].slice(0, 8))
  }

  function toggleSaved(item: Media) {
    const isSaved = savedIds.includes(item.id)
    setSavedIds(current => isSaved ? current.filter(id => id !== item.id) : [item.id, ...current])
    setToast(isSaved ? `${item.title} removed from My List` : `${item.title} added to My List`)
  }

  const navItems: { id: Section; label: string }[] = [
    { id: 'home', label: 'Home' },
    { id: 'movies', label: 'Movies' },
    { id: 'series', label: 'Serials' },
    { id: 'my-list', label: 'My List' },
    { id: 'pricing', label: 'Support' }
  ]

  return (
    <div className="app">
      <a className="skip-link" href="#main-content">Skip to content</a>
      <header>
        <button className="logo" onClick={() => navigate('home')} aria-label="OpenFlix home"><span>OPEN</span>FLIX<i /></button>
        <nav aria-label="Primary navigation">
          {navItems.map(item => <button className={active === item.id ? 'active' : ''} onClick={() => navigate(item.id)} key={item.id}>{item.label}</button>)}
        </nav>
        <div className="head-actions">
          <label className="search">
            <Search size={17} />
            <input value={query} onChange={event => setQuery(event.target.value)} placeholder="Search films, genres…" aria-label="Search the catalog" />
            {query && <button onClick={() => setQuery('')} aria-label="Clear search"><X size={15} /></button>}
          </label>
          {user ? <button className="avatar" aria-label={`${user.displayName} account`}>{user.displayName[0]}</button> : <button className="signin" onClick={() => setAuthOpen(true)}>Sign in</button>}
          <button className="hamburger" onClick={() => setMobileOpen(true)} aria-label="Open menu" aria-expanded={mobileOpen}><Menu /></button>
        </div>
      </header>

      {mobileOpen && (
        <div className="mobile-drawer" role="dialog" aria-modal="true" aria-label="Navigation menu">
          <div className="mobile-drawer-top">
            <span className="logo"><span>OPEN</span>FLIX<i /></span>
            <button onClick={() => setMobileOpen(false)} aria-label="Close menu"><X /></button>
          </div>
          <nav>
            {navItems.map(item => <button className={active === item.id ? 'active' : ''} onClick={() => navigate(item.id)} key={item.id}>{item.label}<span>→</span></button>)}
          </nav>
          <p>Curated public-domain and openly licensed cinema, with source notes beside every title.</p>
        </div>
      )}

      {!query && active === 'home' && (
        <section className="hero" style={{ backgroundImage: `linear-gradient(90deg,rgba(6,6,8,.98) 0%,rgba(6,6,8,.72) 44%,rgba(6,6,8,.04) 80%),linear-gradient(0deg,#070709 0%,transparent 42%),url(${data.featured.backdropUrl})` }}>
          <div className="hero-copy">
            <span className="eyebrow"><Sparkles size={14} /> RESTORED CLASSIC</span>
            <h1>{data.featured.title}</h1>
            <div className="facts">
              <b><ShieldCheck size={14} /> Open access</b>
              <span>{data.featured.year}</span>
              <span className="rating">{data.featured.maturityRating}+</span>
              <span>{data.featured.genre}</span>
            </div>
            <p>{data.featured.synopsis}</p>
            <div className="hero-actions">
              <button className="primary" onClick={() => play(data.featured)}><Play fill="currentColor" /> Play now</button>
              <button className="secondary" onClick={() => setDetails(data.featured)}><Info /> More info</button>
            </div>
            <small className="source"><ShieldCheck size={14} /> Source and rights note provided with every title</small>
          </div>
          <span className="scroll-cue">SCROLL TO DISCOVER <ChevronDown /></span>
        </section>
      )}

      <main id="main-content" className={!query && active === 'home' ? 'overlap' : ''}>
        {query ? (
          <section className="search-page">
            <div className="browse-heading">
              <span className="eyebrow"><Search size={14} /> CATALOG SEARCH</span>
              <h1>Results for “{query}”</h1>
              <p>{searchResults.length ? `${searchResults.length} matching ${searchResults.length === 1 ? 'title' : 'titles'} across the open catalog.` : 'Try another title, year, or genre.'}</p>
            </div>
            {searchResults.length ? (
              <div className="catalog-grid">
                {searchResults.map(item => (
                  <Card key={item.id} item={item} onPlay={play} onDetails={setDetails} saved={savedIds.includes(item.id)} onToggleSaved={toggleSaved} />
                ))}
              </div>
            ) : (
              <EmptyState icon={<Search />} title="No films found" copy="Try a broader search such as “Comedy,” “Adventure,” or “1926.”" action={<button className="secondary" onClick={() => setQuery('')}>Clear search</button>} />
            )}
          </section>
        ) : active === 'pricing' ? (
          <Pricing plans={plans} subscription={subscription} signedIn={Boolean(user)} signIn={() => setAuthOpen(true)} />
        ) : active === 'movies' ? (
          <BrowseView title="Feature films" copy="Landmark comedies, mysteries, horror, and early science fiction—presented with their source history intact." items={data.movies} selectedGenre={selectedGenre} setSelectedGenre={setSelectedGenre} onPlay={play} onDetails={setDetails} savedIds={savedIds} onToggleSaved={toggleSaved} />
        ) : active === 'series' ? (
          data.series.length ? (
            <BrowseView title="Serial adventures" copy="Return to the cliffhangers and chapter plays that helped shape serialized screen storytelling." items={data.series} selectedGenre={selectedGenre} setSelectedGenre={setSelectedGenre} onPlay={play} onDetails={setDetails} savedIds={savedIds} onToggleSaved={toggleSaved} />
          ) : (
            <EmptyState icon={<Film />} title="More serials are being reviewed" copy="We only publish a serial after its source and rights metadata have been checked." action={<button className="primary" onClick={() => navigate('movies')}>Browse films</button>} />
          )
        ) : active === 'my-list' ? (
          <section className="browse-page">
            <div className="browse-heading">
              <span className="eyebrow"><Heart size={14} /> YOUR COLLECTION</span>
              <h1>My List</h1>
              <p>Your saved films stay on this device, ready whenever you return.</p>
            </div>
            {savedTitles.length ? (
              <div className="catalog-grid">
                {savedTitles.map(item => <Card key={item.id} item={item} onPlay={play} onDetails={setDetails} saved onToggleSaved={toggleSaved} />)}
              </div>
            ) : (
              <EmptyState icon={<Bookmark />} title="Your list is waiting" copy="Save a title from any film card and it will appear here." action={<button className="primary" onClick={() => navigate('movies')}>Discover films</button>} />
            )}
          </section>
        ) : (
          <>
            {recentTitles.length > 0 && <Row title="Recently played" eyebrow="PICK UP A CLASSIC" items={recentTitles} onPlay={play} onDetails={setDetails} savedIds={savedIds} onToggleSaved={toggleSaved} />}
            <Row title="Essential open cinema" eyebrow="CURATOR’S SELECTION" items={allTitles} onPlay={play} onDetails={setDetails} savedIds={savedIds} onToggleSaved={toggleSaved} onExplore={() => navigate('movies')} />
            <section className="editorial">
              <div className="editorial-copy">
                <span className="eyebrow">WHY THIS FILM MATTERS</span>
                <h2>Comedy engineered with impossible precision.</h2>
                <p>Buster Keaton’s <em>The General</em> turns locomotives, landscapes, and split-second timing into one of silent cinema’s most ambitious adventures.</p>
                <button className="secondary" onClick={() => setDetails(allTitles.find(item => item.title === 'The General') ?? data.featured)}><Info size={18} /> Read the film note</button>
              </div>
              <div className="editorial-art" style={{ backgroundImage: `url(${allTitles.find(item => item.title === 'The General')?.backdropUrl})` }}>
                <span><Clock3 size={16} /> 1926 · Silent comedy</span>
              </div>
            </section>
            {data.genres.filter(row => row.items.length > 1).map(row => (
              <Row key={row.genre} title={row.genre} items={row.items} onPlay={play} onDetails={setDetails} savedIds={savedIds} onToggleSaved={toggleSaved} />
            ))}
            <section className="manifesto">
              <span className="eyebrow">CINEMA WORTH PRESERVING</span>
              <h2>Great stories belong in the light.</h2>
              <p>OpenFlix pairs every title with its original source and rights context, making discovery feel as responsible as it is cinematic.</p>
              <div className="manifesto-points">
                <span><ShieldCheck /> Transparent sources</span>
                <span><Film /> Curated classics</span>
                <span><Heart /> Culture kept open</span>
              </div>
            </section>
          </>
        )}
      </main>

      <footer>
        <button className="logo" onClick={() => navigate('home')}><span>OPEN</span>FLIX</button>
        <p>Streaming cinema with respect for creators and the public domain.</p>
        <div>
          <span className={`status-dot ${catalogOnline === true ? 'online' : ''}`} />
          {catalogOnline === true ? 'Catalog connected' : catalogOnline === false ? 'Curated offline catalog' : 'Checking catalog'}
        </div>
        <small>© 2026 OpenFlix · MIT software</small>
      </footer>

      {toast && <div className="toast" role="status"><Check size={17} /> {toast}</div>}
      {catalogOnline === false && <div className="sr-only" role="status"><CircleAlert /> Live catalog unavailable. Showing the curated offline catalog.</div>}
      {authOpen && <AuthModal close={() => setAuthOpen(false)} success={result => {
        setUser(result.user)
        setAuthOpen(false)
        api.subscription().then(setSubscription).catch(() => {})
      }} />}
      {details && <DetailsModal item={details} saved={savedIds.includes(details.id)} close={() => setDetails(null)} play={() => play(details)} toggleSaved={() => toggleSaved(details)} />}
      {player && <Player item={player} close={() => setPlayer(null)} />}
    </div>
  )
}
