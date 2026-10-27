import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { eraFor } from '../game/eras';
import { EVENT_LABELS } from './admin-audit.component';

interface Count { label: string; count: number; }
interface Stats {
  from: string; to: string;
  totals:   { visits: number; visitors: number; newVisitors: number; games: number; phonePercent: number };
  previous: { visits: number; visitors: number; games: number; phonePercent: number } | null;
  perDay: { date: string; count: number }[];
  byHour: number[];
  byWeekday: number[];
  actions: Count[]; sources: Count[]; topPlayers: Count[];
  hardest: { label: string; album: string; plays: number; misses: number }[];
  albums:  { album: string; plays: number; correct: number }[];
}

@Component({
  selector: 'swiftgame-admin-stats',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './admin-stats.component.html',
  styleUrl: './admin-insights.scss'
})
export class AdminStatsComponent implements OnInit {
  readonly ranges = [{ d: 7, l: '7d' }, { d: 30, l: '30d' }, { d: 90, l: '90d' }, { d: 365, l: '1y' }, { d: 0, l: 'All' }];
  readonly hours = Array.from({ length: 24 }, (_, i) => i);
  readonly weekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  days = 30;
  stats: Stats | null = null;
  loading = false;
  error = '';

  constructor(private http: HttpClient) {}

  ngOnInit(): void { this.load(); }

  setRange(d: number): void { if (d !== this.days) { this.days = d; this.load(); } }

  load(): void {
    this.loading = true;
    this.http.get<Stats>(`${environment.apiUrl}/admin/stats`, { params: { days: this.days } }).subscribe({
      next: s => { this.stats = s; this.loading = false; this.error = ''; },
      error: () => { this.error = "Couldn't load stats. Try again in a moment."; this.loading = false; }
    });
  }

  max(values: number[]): number { return Math.max(1, ...values); }
  pct(v: number, max: number): number { return Math.round((v / max) * 100); }
  peak(values: number[]): number { return values.indexOf(Math.max(...values)); }
  counts(list: Count[]): number[] { return list.map(c => c.count); }
  actionLabel(t: string): string { return EVENT_LABELS[t] ?? t; }

  delta(cur: number, prev: number | undefined): string {
    if (prev === undefined || this.days === 0) return '';
    if (prev === 0) return cur > 0 ? 'new' : '';
    const p = Math.round(((cur - prev) / prev) * 100);
    return p === 0 ? 'same' : `${p > 0 ? '+' : ''}${p}%`;
  }

  // Album accuracy rolled up into eras, weakest first
  get eras(): { label: string; color: string; plays: number; accuracy: number }[] {
    const map = new Map<string, { label: string; color: string; plays: number; correct: number }>();
    for (const a of this.stats?.albums ?? []) {
      const e = eraFor(a.album);
      const row = map.get(e.key) ?? { label: e.label || a.album, color: e.color, plays: 0, correct: 0 };
      row.plays += a.plays; row.correct += a.correct;
      map.set(e.key, row);
    }
    return [...map.values()]
      .map(r => ({ ...r, accuracy: Math.round((r.correct / Math.max(1, r.plays)) * 100) }))
      .sort((a, b) => a.accuracy - b.accuracy);
  }

  eraColor(album: string): string { return eraFor(album).color; }

  dayLabel(iso: string): string {
    return new Date(iso + 'T00:00:00').toLocaleDateString(undefined, { day: 'numeric', month: 'short' });
  }
}
