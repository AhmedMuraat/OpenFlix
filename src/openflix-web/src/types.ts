export type MediaKind = 0 | 1
export interface Media {
  id: string; title: string; synopsis: string; genre: string; posterUrl: string; backdropUrl: string;
  streamUrl: string; sourceUrl: string; license: string; attribution: string; kind: MediaKind;
  year: number; maturityRating: number; views: number; featured: boolean
}
export interface HomeData {
  featured: Media; movies: Media[]; series: Media[];
  genres: { genre: string; items: Media[] }[]
}
export interface User { id: string; email: string; displayName: string; roles: string[] }
export interface AuthResponse { accessToken: string; refreshToken: string; expiresAt: string; user: User }
export interface Plan { id: string; name: string; price: number; features: string[] }
export interface Subscription { status: number; currentPeriodEnd?: string }
