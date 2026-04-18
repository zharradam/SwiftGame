import { Component, OnInit, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../services/auth.service';

export interface PlayerRecord {
  id:          string;
  username:    string;
  email:       string;
  isAdmin:     boolean;
  isModerator: boolean;
  isBanned:    boolean;
  createdAt:   string;
}

@Component({
  selector: 'swiftgame-admin-panel',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './admin-panel.component.html',
  styleUrl: './admin-panel.component.scss'
})
export class AdminPanelComponent implements OnInit {
  @Output() close = new EventEmitter<void>();

  players: PlayerRecord[] = [];
  loading: boolean        = true;
  error:   string         = '';

  private readonly apiUrl = `${environment.apiUrl}/admin`;

  constructor(private http: HttpClient, readonly auth: AuthService) {}

  ngOnInit(): void { this.loadPlayers(); }

  loadPlayers(): void {
    this.loading = true;
    this.http.get<PlayerRecord[]>(`${this.apiUrl}/players`).subscribe({
      next:  players => { this.players = players; this.loading = false; },
      error: ()      => { this.error = 'Failed to load players.'; this.loading = false; }
    });
  }

  ban(player: PlayerRecord): void {
    this.http.post(`${this.apiUrl}/players/${player.id}/ban`, {}).subscribe({
      next:  () => this.loadPlayers(),
      error: () => this.error = 'Failed to ban player.'
    });
  }

  unban(player: PlayerRecord): void {
    this.http.post(`${this.apiUrl}/players/${player.id}/unban`, {}).subscribe({
      next:  () => this.loadPlayers(),
      error: () => this.error = 'Failed to unban player.'
    });
  }

  toggleModerator(player: PlayerRecord): void {
    this.http.post(`${this.apiUrl}/players/${player.id}/moderator`, {}).subscribe({
      next:  () => this.loadPlayers(),
      error: () => this.error = 'Failed to update moderator status.'
    });
  }
}