import { Injectable, effect } from '@angular/core';
import { AuthService } from './auth.service';
import { eraByKey } from '../components/game/eras';

// Fan titles: each era is a level, earned by total points across all games
export const TITLES: Array<{ at: number; title: string }> = [
  { at: 0,         title: 'Debut' },
  { at: 50_000,    title: 'Fearless' },
  { at: 150_000,   title: 'Speak Now' },
  { at: 300_000,   title: 'Red' },
  { at: 500_000,   title: '1989' },
  { at: 800_000,   title: 'reputation' },
  { at: 1_200_000, title: 'Lover' },
  { at: 1_700_000, title: 'folklore' },
  { at: 2_300_000, title: 'evermore' },
  { at: 3_000_000, title: 'Midnights' },
  { at: 4_000_000, title: 'Tortured Poet' },
  { at: 5_000_000, title: 'Mastermind' },
];

export function titleFor(points: number): { title: string; next: { at: number; title: string } | null; progress: number } {
  let i = 0;
  while (i + 1 < TITLES.length && points >= TITLES[i + 1].at) i++;
  const next = TITLES[i + 1] ?? null;
  const progress = next ? (points - TITLES[i].at) / (next.at - TITLES[i].at) : 1;
  return { title: TITLES[i].title, next, progress: Math.max(0, Math.min(1, progress)) };
}

// Era skin: the player's favourite era recolours the accents and the page glow
@Injectable({ providedIn: 'root' })
export class SkinService {
  constructor(auth: AuthService) {
    effect(() => this.apply(auth.user()?.favouriteEra ?? null));
  }

  apply(eraKey: string | null): void {
    const root = document.documentElement;
    const era = eraKey ? eraByKey(eraKey) : null;
    if (!era) {
      root.style.removeProperty('--skin-1');
      root.style.removeProperty('--skin-2');
      root.removeAttribute('data-skin');
      return;
    }
    root.style.setProperty('--skin-1', era.color);
    root.style.setProperty('--skin-2', `color-mix(in srgb, ${era.color} 45%, #4c1d95)`);
    root.setAttribute('data-skin', era.key);
  }
}
