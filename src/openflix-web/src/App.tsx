import { useEffect, useMemo, useState } from 'react'
import { ChevronDown, Info, Menu, Play, Search, Sparkles, X } from 'lucide-react'
import { api } from './api'
import type { AuthResponse, HomeData, Media, Plan, Subscription } from './types'

const fallback: HomeData = {
  featured: { id:'1', title:'Night of the Living Dead', synopsis:'Seven strangers fight to survive a terrifying night in a rural farmhouse.', genre:'Horror', year:1968, kind:0, maturityRating:16, views:9421, featured:true, posterUrl:'https://archive.org/services/img/NightOfTheLivingDead', backdropUrl:'https://archive.org/services/img/NightOfTheLivingDead', streamUrl:'https://archive.org/embed/NightOfTheLivingDead?autoplay=1', sourceUrl:'https://archive.org/details/NightOfTheLivingDead', license:'Public domain — verify source metadata', attribution:'Internet Archive' },
  movies: [], series: [], genres: []
}

function Card({ item, onPlay }: { item: Media; onPlay: (m: Media) => void }) {
  return <button className="card" onClick={() => onPlay(item)} aria-label={`Play ${item.title}`}>
    <img src={item.posterUrl} alt="" loading="lazy" />
    <span className="card-shade" />
    <span className="card-copy"><b>{item.title}</b><small>{item.year} · {item.genre}</small></span>
    <span className="card-play"><Play fill="currentColor" size={16}/></span>
  </button>
}

function Row({ title, items, onPlay }: { title: string; items: Media[]; onPlay: (m: Media) => void }) {
  if (!items.length) return null
  return <section className="row"><div className="row-title"><h2>{title}</h2><button>Explore all <span>→</span></button></div>
    <div className="rail">{items.map(item => <Card key={item.id} item={item} onPlay={onPlay}/>)}</div></section>
}

function AuthModal({ close, success }: { close: () => void; success: (auth: AuthResponse) => void }) {
  const [register, setRegister] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError('')
    const data = new FormData(event.currentTarget)
    try {
      const result = register
        ? await api.register(String(data.get('email')), String(data.get('password')), String(data.get('name')))
        : await api.login(String(data.get('email')), String(data.get('password')))
      api.setToken(result.accessToken); success(result)
    } catch (e) { setError(e instanceof Error ? e.message : 'Try again.') } finally { setBusy(false) }
  }
  return <div className="modal-backdrop" role="dialog" aria-modal="true"><div className="auth-card">
    <button className="close" onClick={close} aria-label="Close"><X/></button>
    <span className="eyebrow">YOUR CINEMA AWAITS</span><h2>{register ? 'Create your account' : 'Welcome back'}</h2>
    <p>{register ? 'Join a community preserving cinema history.' : 'Continue where the story left off.'}</p>
    <form onSubmit={submit}>
      {register && <label>Display name<input name="name" required minLength={2}/></label>}
      <label>Email<input name="email" type="email" autoComplete="email" required/></label>
      <label>Password<input name="password" type="password" autoComplete={register ? 'new-password' : 'current-password'} required minLength={12}/></label>
      {error && <div className="error" role="alert">{error}</div>}
      <button className="primary wide" disabled={busy}>{busy ? 'Please wait…' : register ? 'Create account' : 'Sign in'}</button>
    </form>
    <button className="switch" onClick={() => setRegister(!register)}>{register ? 'Already a member? Sign in' : 'New here? Create an account'}</button>
  </div></div>
}

function Player({ item, close }: { item: Media; close: () => void }) {
  useEffect(() => { api.view(item.id).catch(() => {}) }, [item.id])
  return <div className="player-modal" role="dialog" aria-modal="true">
    <button className="player-close" onClick={close} aria-label="Close player"><X/></button>
    <iframe src={item.streamUrl} title={item.title} allowFullScreen allow="autoplay; fullscreen"/>
    <div className="player-meta"><div><span className="eyebrow">{item.genre} · {item.year}</span><h2>{item.title}</h2><p>{item.synopsis}</p></div>
      <div className="rights"><b>Source & rights</b><span>{item.license}</span><a href={item.sourceUrl} target="_blank">View original source ↗</a></div></div>
  </div>
}

function Pricing({ plans, subscription, signedIn, signIn }: {
  plans: Plan[]; subscription: Subscription | null; signedIn: boolean; signIn: () => void
}) {
  const [busy, setBusy] = useState(false)
  const isSupporter = subscription?.status === 1 || subscription?.status === 2
  async function subscribe() {
    if (!signedIn) { signIn(); return }
    setBusy(true)
    try {
      const result = await api.checkout(`${location.origin}/?checkout=success`, `${location.origin}/?checkout=cancelled`)
      location.assign(result.url)
    } finally { setBusy(false) }
  }
  return <section className="pricing-page">
    <span className="eyebrow">SUPPORT OPEN CINEMA</span>
    <h1>Simple plans. No locked-up culture.</h1>
    <p>Watch the open catalog free. Supporters fund careful rights review and new features.</p>
    <div className="plans">{plans.map(plan => <article className={plan.id === 'supporter' ? 'plan featured-plan' : 'plan'} key={plan.id}>
      <span>{plan.name}</span><h2>{plan.price ? `€${plan.price}` : 'Free'}<small>{plan.price ? '/month' : ''}</small></h2>
      <ul>{plan.features.map(feature => <li key={feature}>✓ {feature}</li>)}</ul>
      {plan.id === 'supporter'
        ? <button className="primary wide" onClick={subscribe} disabled={busy || isSupporter}>{isSupporter ? 'Active supporter' : busy ? 'Opening checkout…' : 'Become a supporter'}</button>
        : <button className="secondary wide" onClick={() => location.assign('/')}>Browse free</button>}
    </article>)}</div>
    <small>Payments are handled by Stripe. OpenFlix never stores card details.</small>
  </section>
}

export default function App() {
  const [data, setData] = useState<HomeData>(fallback)
  const [active, setActive] = useState<'home'|'movies'|'series'|'pricing'>('home')
  const [player, setPlayer] = useState<Media | null>(null)
  const [authOpen, setAuthOpen] = useState(false)
  const [user, setUser] = useState<AuthResponse['user'] | null>(null)
  const [query, setQuery] = useState('')
  const [loading, setLoading] = useState(true)
  const [plans, setPlans] = useState<Plan[]>([])
  const [subscription, setSubscription] = useState<Subscription | null>(null)
  useEffect(() => {
    Promise.all([api.home().then(setData), api.plans().then(setPlans)]).catch(() => {}).finally(() => setLoading(false))
  }, [])
  const visible = useMemo(() => {
    const base = active === 'movies' ? data.movies : active === 'series' ? data.series : [...data.movies, ...data.series]
    return query ? base.filter(x => `${x.title} ${x.genre}`.toLowerCase().includes(query.toLowerCase())) : base
  }, [active, data, query])
  const play = (item: Media) => setPlayer(item)
  return <div className="app">
    <header><a className="logo" href="#"><span>OPEN</span>FLIX<i/></a>
      <nav>{(['home','movies','series','pricing'] as const).map(tab => <button className={active===tab?'active':''} onClick={()=>setActive(tab)} key={tab}>{tab}</button>)}</nav>
      <div className="head-actions"><label className="search"><Search size={18}/><input value={query} onChange={e=>setQuery(e.target.value)} placeholder="Titles, genres…"/></label>
        {user ? <button className="avatar">{user.displayName[0]}</button> : <button className="signin" onClick={()=>setAuthOpen(true)}>Sign in</button>}
        <button className="hamburger" aria-label="Menu"><Menu/></button></div>
    </header>

    {active === 'home' && !query && <section className="hero" style={{backgroundImage:`linear-gradient(90deg,rgba(6,6,8,.98) 0%,rgba(6,6,8,.74) 43%,rgba(6,6,8,.06) 78%),linear-gradient(0deg,#070709 0%,transparent 38%),url(${data.featured.backdropUrl})`}}>
      <div className="hero-copy"><span className="eyebrow"><Sparkles size={14}/> RESTORED CLASSIC</span><h1>{data.featured.title}</h1>
        <div className="facts"><b>98% Match</b><span>{data.featured.year}</span><span className="rating">{data.featured.maturityRating}+</span><span>{data.featured.genre}</span></div>
        <p>{data.featured.synopsis}</p><div className="hero-actions"><button className="primary" onClick={()=>play(data.featured)}><Play fill="currentColor"/>Play now</button>
          <button className="secondary"><Info/>More info</button></div><small className="source">Openly accessible · Source attribution provided</small></div>
      <span className="scroll-cue">SCROLL TO DISCOVER <ChevronDown/></span>
    </section>}

    <main className={active==='home'&&!query?'overlap':''}>
      {active === 'pricing' ? <Pricing plans={plans} subscription={subscription} signedIn={!!user} signIn={()=>setAuthOpen(true)}/> : loading ? <div className="loading">Curating tonight’s cinema…</div> : query
        ? <Row title={`Results for “${query}”`} items={visible} onPlay={play}/>
        : active==='home' ? <>
          <Row title="Trending now" items={data.movies} onPlay={play}/>
          <Row title="Serial adventures" items={data.series} onPlay={play}/>
          {data.genres.map(row=><Row key={row.genre} title={row.genre} items={row.items} onPlay={play}/>)}
          <section className="manifesto"><span className="eyebrow">CINEMA WORTH PRESERVING</span><h2>Great stories belong in the light.</h2><p>OpenFlix curates public-domain and openly licensed cinema, with a source and rights note beside every title.</p></section>
        </> : <Row title={active==='movies'?'Movies':'Series'} items={visible} onPlay={play}/>}
    </main>
    <footer><a className="logo" href="#"><span>OPEN</span>FLIX</a><p>Streaming cinema with respect for creators and the public domain.</p><span>© 2026 OpenFlix · MIT software</span></footer>
    {authOpen && <AuthModal close={()=>setAuthOpen(false)} success={result=>{
      setUser(result.user);setAuthOpen(false);api.subscription().then(setSubscription).catch(()=>{})
    }}/>}
    {player && <Player item={player} close={()=>setPlayer(null)}/>}
  </div>
}
