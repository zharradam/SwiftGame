import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

interface AuditRow {
  id: number; at: string; type: string;
  playerName: string | null; source: string | null; device: string | null;
  detail: string | null; visitor: string | null;
}

export const EVENT_LABELS: Record<string, string> = {
  visit: 'Visited', register: 'Signed up', login: 'Logged in', logout: 'Logged out',
  game_start: 'Started a game', game_end: 'Finished a game', chat: 'Chatted', admin: 'Admin action'
};

@Component({
  selector: 'swiftgame-admin-audit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="audit-filters">
      <select [(ngModel)]="type" (ngModelChange)="reload()" aria-label="Event type">
        <option value="">All events</option>
        @for (t of types; track t) { <option [value]="t">{{ label(t) }}</option> }
      </select>
      <input [(ngModel)]="player" (keyup.enter)="reload()" (blur)="reload()" placeholder="Player name" aria-label="Filter by player" />
    </div>

    @if (error) { <div class="admin-error">{{ error }}</div> }

    <div class="admin-list audit-list">
      @for (r of rows; track r.id) {
        <div class="audit-row">
          <span class="audit-type audit-type--{{ r.type }}">{{ label(r.type) }}</span>
          <span class="audit-who">{{ r.playerName ?? (r.visitor ? 'Visitor ' + r.visitor : 'Someone') }}</span>
          <span class="audit-detail">
            @if (r.type === 'visit') { from {{ r.source }} · {{ r.device }} } @else { {{ r.detail }} }
          </span>
          <time class="audit-when" [attr.datetime]="r.at" [title]="r.at + 'Z' | date:'medium'">{{ ago(r.at) }}</time>
        </div>
      } @empty {
        @if (!loading) { <p class="admin-loading">Nothing recorded yet.</p> }
      }
    </div>

    @if (loading) { <div class="admin-loading">Loading…</div> }
    @if (!loading && more) { <button class="admin-btn audit-more" (click)="load()">Show older</button> }
  `,
  styleUrl: './admin-insights.scss'
})
export class AdminAuditComponent implements OnInit {
  rows: AuditRow[] = [];
  type = '';
  player = '';
  loading = false;
  more = true;
  error = '';
  readonly types = Object.keys(EVENT_LABELS);
  private lastQuery = '';

  constructor(private http: HttpClient) {}

  ngOnInit(): void { this.load(); }

  label(t: string): string { return EVENT_LABELS[t] ?? t; }

  reload(): void {
    const q = `${this.type}|${this.player}`;
    if (q === this.lastQuery && this.rows.length) return;
    this.rows = [];
    this.more = true;
    this.load();
  }

  load(): void {
    this.lastQuery = `${this.type}|${this.player}`;
    this.loading = true;
    const params: Record<string, string> = { take: '50' };
    if (this.type) params['type'] = this.type;
    if (this.player.trim()) params['player'] = this.player.trim();
    if (this.rows.length) params['before'] = String(this.rows[this.rows.length - 1].id);

    this.http.get<AuditRow[]>(`${environment.apiUrl}/admin/audit`, { params }).subscribe({
      next: rows => { this.rows = [...this.rows, ...rows]; this.more = rows.length === 50; this.loading = false; this.error = ''; },
      error: () => { this.error = "Couldn't load the audit log. Try again in a moment."; this.loading = false; }
    });
  }

  ago(at: string): string {
    const s = Math.max(0, (Date.now() - new Date(at + 'Z').getTime()) / 1000);
    if (s < 60) return 'just now';
    if (s < 3600) return `${Math.floor(s / 60)} min ago`;
    if (s < 86400) return `${Math.floor(s / 3600)} h ago`;
    if (s < 86400 * 7) return `${Math.floor(s / 86400)} d ago`;
    return new Date(at + 'Z').toLocaleDateString(undefined, { day: 'numeric', month: 'short' });
  }
}
