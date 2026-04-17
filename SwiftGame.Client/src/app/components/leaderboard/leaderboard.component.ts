import { Component, OnInit, OnDestroy } from '@angular/core';
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
  entries: LeaderboardEntry[]           = [];
  state:   'loading' | 'loaded' | 'error' = 'loading';
  private subscription: Subscription | null = null;

  constructor(
    private gameService:   GameService,
    private signalrService: SignalrService
  ) {}

  ngOnInit(): void {
    this.loadLeaderboard();

    // Listen for real-time updates from any connected client
    this.subscription = this.signalrService.leaderboardUpdated.subscribe(() => {
      this.loadLeaderboard();
    });
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
  }

  loadLeaderboard(): void {
    this.gameService.getLeaderboard(10).subscribe({
      next: (entries) => {
        this.entries = entries;
        this.state   = 'loaded';
      },
      error: (err) => {
        this.state = 'error';
        console.error(err);
      }
    });
  }

  formatTime(ms: number): string {
    const seconds = Math.floor(ms / 1000);
    const tenths  = Math.floor((ms % 1000) / 100);
    return `${seconds}.${tenths}s`;
  }

  getMedal(index: number): string {
    switch(index) {
      case 0: return '🥇';
      case 1: return '🥈';
      case 2: return '🥉';
      default: return `${index + 1}`;
    }
  }
}