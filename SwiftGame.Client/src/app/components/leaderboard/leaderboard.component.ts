import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { GameService, LeaderboardEntry } from '../../services/game.service';
import { SignalrService } from '../../services/signalr.service';

@Component({
  selector: 'swiftgame-leaderboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './leaderboard.component.html',
  styleUrl: './leaderboard.component.scss'
})
export class LeaderboardComponent implements OnInit, OnDestroy {
  entries: LeaderboardEntry[]              = [];
  state:   'loading' | 'loaded' | 'error' = 'loading';

  flashSet    = new Set<string>();       // rows whose score just changed
  rankChanges = new Map<string, number>(); // key → +N moved up / -N moved down
  newSet      = new Set<string>();       // rows that just appeared on the board

  private subscription: Subscription | null = null;
  private clearTimer:   ReturnType<typeof setTimeout> | null = null;

  constructor(
    private gameService:    GameService,
    private signalrService: SignalrService,
    private cdr:            ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadLeaderboard();
    this.subscription = this.signalrService.leaderboardUpdated.subscribe(
      () => this.loadLeaderboard()
    );
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
    if (this.clearTimer) clearTimeout(this.clearTimer);
  }

  loadLeaderboard(): void {
    this.gameService.getLeaderboard(10).subscribe({
      next:  (entries) => { this.diffAndUpdate(entries); this.state = 'loaded'; },
      error: ()        => { this.state = 'error'; }
    });
  }

  // ── Diff logic ──────────────────────────────────────────────────

  private key(e: LeaderboardEntry): string {
    return e.id;
  }

  private diffAndUpdate(next: LeaderboardEntry[]): void {
    const oldRanks  = new Map(this.entries.map((e, i) => [this.key(e), i]));
    const oldScores = new Map(this.entries.map(e   => [this.key(e), e.pointsEarned]));
    const hadData   = this.entries.length > 0;

    const changes  = new Map<string, number>();
    const flashing = new Set<string>();
    const fresh    = new Set<string>();

    next.forEach((entry, newRank) => {
      const k        = this.key(entry);
      const oldRank  = oldRanks.get(k);
      const oldScore = oldScores.get(k);

      if (oldRank === undefined && hadData) {
        fresh.add(k);
      } else if (oldRank !== undefined) {
        const delta = oldRank - newRank;
        if (delta !== 0) changes.set(k, delta);
        if (oldScore !== entry.pointsEarned) flashing.add(k);
      }
    });

    this.rankChanges = changes;
    this.flashSet    = flashing;
    this.newSet      = fresh;
    this.entries     = next;

    if (this.clearTimer) clearTimeout(this.clearTimer);
    this.clearTimer = setTimeout(() => {
      this.rankChanges = new Map();
      this.flashSet    = new Set();
      this.newSet      = new Set();
      this.cdr.markForCheck();
    }, 6500);
  }

  // ── Template helpers ────────────────────────────────────────────

  isFlashing(e: LeaderboardEntry): boolean { return this.flashSet.has(this.key(e)); }
  isNew(e: LeaderboardEntry):      boolean { return this.newSet.has(this.key(e)); }

  getRankChange(e: LeaderboardEntry): number {
    return this.rankChanges.get(this.key(e)) ?? 0;
  }

  getRankChangeLabel(e: LeaderboardEntry): string {
    const n = this.getRankChange(e);
    if (n === 0) return '';
    return (n > 0 ? '▲' : '▼') + Math.abs(n);
  }

  trackByEntry = (_: number, e: LeaderboardEntry): string => this.key(e);

  formatTime(ms: number): string {
    const s = Math.floor(ms / 1000);
    const t = Math.floor((ms % 1000) / 100);
    return `${s}.${t}s`;
  }

  getMedal(i: number): string {
    return ['🥇', '🥈', '🥉'][i] ?? `${i + 1}`;
  }
}