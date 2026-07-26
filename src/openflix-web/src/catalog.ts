import type { HomeData, Media, MediaKind, Plan } from './types'

function archiveTitle(
  id: string,
  title: string,
  synopsis: string,
  genre: string,
  year: number,
  archiveId: string,
  views: number,
  options: { featured?: boolean; kind?: MediaKind; maturityRating?: number } = {}
): Media {
  return {
    id,
    title,
    synopsis,
    genre,
    year,
    views,
    featured: options.featured ?? false,
    kind: options.kind ?? 0,
    maturityRating: options.maturityRating ?? (genre === 'Horror' ? 16 : 12),
    posterUrl: `https://archive.org/services/img/${archiveId}`,
    backdropUrl: `https://archive.org/services/img/${archiveId}`,
    streamUrl: `https://archive.org/embed/${archiveId}?autoplay=1`,
    sourceUrl: `https://archive.org/details/${archiveId}`,
    license: 'Public domain / source-declared open access — verify source metadata',
    attribution: `Hosted by Internet Archive; source uploader metadata for ${title}`
  }
}

const titles: Media[] = [
  archiveTitle(
    '1',
    'Night of the Living Dead',
    'Seven strangers fight to survive a terrifying night in a rural farmhouse while the world outside descends into chaos.',
    'Horror',
    1968,
    'NightOfTheLivingDead',
    9421,
    { featured: true, maturityRating: 16 }
  ),
  archiveTitle(
    '2',
    'His Girl Friday',
    'A newspaper editor uses every trick he knows to keep his ace reporter—and former wife—from remarrying.',
    'Comedy',
    1940,
    'his_girl_friday',
    8110
  ),
  archiveTitle(
    '3',
    'The General',
    'A railway engineer pursues a stolen locomotive through enemy territory in Buster Keaton’s landmark silent comedy.',
    'Comedy',
    1926,
    'TheGeneral1926',
    7032
  ),
  archiveTitle(
    '4',
    'Sherlock Jr.',
    'A projectionist dreams himself into the detective story unfolding on screen and becomes the hero he imagines.',
    'Comedy',
    1924,
    'SherlockJr',
    6604
  ),
  archiveTitle(
    '5',
    'Charade',
    'A woman in Paris is pursued by several men who want the fortune her murdered husband stole.',
    'Mystery',
    1963,
    'Charade1963',
    6129
  ),
  archiveTitle(
    '6',
    'The Phantom Planet',
    'An astronaut is miniaturized on a mysterious asteroid and pulled into a hidden civilization.',
    'Science Fiction',
    1961,
    'ThePhantomPlanet',
    5810
  ),
  archiveTitle(
    '7',
    'Flash Gordon: Space Soldiers',
    'Flash Gordon and his allies battle to save Earth in a classic chapter-play space adventure.',
    'Adventure',
    1936,
    'FlashGordonSpaceSoldiers',
    4730,
    { kind: 1 }
  ),
  archiveTitle(
    '8',
    'The Lost World',
    'Explorers discover an isolated plateau where prehistoric creatures still roam.',
    'Adventure',
    1925,
    'TheLostWorld1925',
    3920
  )
]

const movies = titles.filter(title => title.kind === 0)
const series = titles.filter(title => title.kind === 1)

export const fallbackCatalog: HomeData = {
  featured: titles[0],
  movies,
  series,
  genres: [...new Set(titles.map(title => title.genre))].map(genre => ({
    genre,
    items: titles.filter(title => title.genre === genre)
  }))
}

export const fallbackPlans: Plan[] = [
  {
    id: 'free',
    name: 'Open access',
    price: 0,
    features: ['Watch the complete open catalog', 'Source and rights notes', 'Personal watchlist on this device']
  },
  {
    id: 'supporter',
    name: 'Supporter',
    price: 5,
    available: false,
    features: ['Everything in Open access', 'Help fund rights review', 'Support preservation-focused features']
  }
]
