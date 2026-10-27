import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

const VISITOR_KEY = 'swiftgame_visitor';

// Anonymous visitor id + one "visit" event per page load (the server dedupes within 30 minutes).
@Injectable({ providedIn: 'root' })
export class VisitService {
  readonly visitorId = this.loadVisitorId();

  constructor(private http: HttpClient) {}

  recordVisit(): void {
    this.http.post(`${environment.apiUrl}/events/visit`, {
      visitorId: this.visitorId,
      source:    sourceOf(location.search, document.referrer),
      device:    deviceOf()
    }).subscribe({ error: () => {} });   // analytics must never bother the player
  }

  private loadVisitorId(): string {
    try {
      const existing = localStorage.getItem(VISITOR_KEY);
      if (existing) return existing;
      const id = crypto.randomUUID();
      localStorage.setItem(VISITOR_KEY, id);
      return id;
    } catch {
      return crypto.randomUUID();     // private mode: counts as a new visitor each load
    }
  }
}

export function sourceOf(search: string, referrer: string, ownHost = globalThis.location?.hostname ?? ''): string {
  const from = new URLSearchParams(search).get('from');
  if (from === 'share') return 'Shared link';
  if (from) return from.slice(0, 60);

  let host = '';
  try { host = new URL(referrer).hostname.replace(/^www\./, ''); } catch { return 'Direct'; }
  if (!host || host.endsWith('swiftology.uk') || host === ownHost) return 'Direct';

  const known: Array<[RegExp, string]> = [
    [/google\./, 'Google'], [/bing\./, 'Bing'], [/duckduckgo\./, 'DuckDuckGo'],
    [/facebook\.|fb\.|messenger\./, 'Facebook'], [/instagram\./, 'Instagram'],
    [/tiktok\./, 'TikTok'], [/^t\.co$|twitter\.|x\.com/, 'X'], [/reddit\./, 'Reddit'],
    [/whatsapp\./, 'WhatsApp'], [/youtube\./, 'YouTube'], [/github\./, 'GitHub']
  ];
  return known.find(([re]) => re.test(host))?.[1] ?? host;
}

function deviceOf(): 'phone' | 'tablet' | 'desktop' {
  const touch = matchMedia('(pointer: coarse)').matches;
  const w = Math.min(screen.width, screen.height);
  if (touch && w < 600) return 'phone';
  if (touch) return 'tablet';
  return 'desktop';
}
