import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface RoundResponse {
  songId: string;
  songDbId: string;
  provider: string;
  previewUrl: string;
  startAt: number;
  choices: string[];
  kind: 'song' | 'era';
}

export type GameMode = 'classic' | 'daily' | 'challenge' | 'album' | 'eras';

export interface SongSet {
  mode: 'daily' | 'challenge';
  songIds: string[];
  date?: string;
  from?: string;
  challenger?: string;
  challengerScore?: number;
  challengerRounds?: { isCorrect: boolean; pointsEarned: number }[];
}

export interface AlbumOption { id: string; name: string; songs: number; era: string | null; }

export interface Progress {
  avatar: string | null;
  favouriteEra: string | null;
  totalPoints: number;
  games: number;
  streak: number;
  playedDailyToday: boolean;
  beads: { key: string; label: string; correct: number; earned: boolean }[];
  beadAt: number;
  goldBeads: number;
}

export interface LeaderboardEntry {
  id:            string;
  playerName:    string;
  avatar?:       string | null;
  era?:          string | null;
  songTitle:     string;
  pointsEarned:  number;
  responseTimeMs: number;
  playedAt:      string;
}

export type LeaderboardPeriod = 'all' | 'today' | 'daily';

export interface MyBest {
  rank:  number;
  entry: LeaderboardEntry;
}

export interface GameConfig {
  questionsPerGame: number;
  imageBaseUrl:     string;
  imageCount:       number;
}

export interface SubmitAnswerRequest {
  songId:         string;
  provider:       string;
  selectedTitle:  string;
  responseTimeMs: number;
  sessionId:      string;
  playerId:       string;
}

export interface SubmitAnswerResponse {
  isCorrect: boolean;
  pointsEarned: number;
  correctTitle: string;
  responseTimeMs: number;
  album: string;
  albumArt: string;
  correctEra?: string;
}

export interface StartSessionResponse {
  sessionId: string;
  playerId:  string;
}

export interface EndSessionResponse {
  mode?:        GameMode;
  rank:         number;
  totalPlayers: number;
  playerId:     string;
}

@Injectable({
  providedIn: 'root'
})
export class GameService {
  private readonly apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getRound(excludeIds: string[] = [], opts: { songId?: string; albums?: string[]; kind?: 'era' } = {}): Observable<RoundResponse> {
    const params: Record<string, string> = {};
    if (excludeIds.length) params['excludeIds'] = excludeIds.join(',');
    if (opts.songId) params['songId'] = opts.songId;
    if (opts.albums?.length) params['albums'] = opts.albums.join(',');
    if (opts.kind) params['kind'] = opts.kind;
    return this.http.get<RoundResponse>(`${this.apiUrl}/game/round`, { params });
  }

  getSet(mode: 'daily' | 'challenge', from?: string): Observable<SongSet> {
    const params: Record<string, string> = { mode };
    if (from) params['from'] = from;
    return this.http.get<SongSet>(`${this.apiUrl}/game/set`, { params });
  }

  getAlbums(): Observable<AlbumOption[]> {
    return this.http.get<AlbumOption[]>(`${this.apiUrl}/game/albums`);
  }

  getProgress(playerId: string): Observable<Progress> {
    return this.http.get<Progress>(`${this.apiUrl}/players/${playerId}/progress`);
  }

  updateProfile(avatar: string | null, favouriteEra: string | null): Observable<{ avatar: string | null; favouriteEra: string | null }> {
    return this.http.put<{ avatar: string | null; favouriteEra: string | null }>(`${this.apiUrl}/players/me/profile`, { avatar, favouriteEra });
  }

  getAvatars(): Observable<{ avatars: string[] }> {
    return this.http.get<{ avatars: string[] }>(`${this.apiUrl}/players/avatars`);
  }

  submitAnswer(request: SubmitAnswerRequest): Observable<SubmitAnswerResponse> {
    return this.http.post<SubmitAnswerResponse>(
      `${this.apiUrl}/game/submit`,
      request
    );
  }

  getLeaderboard(count: number = 10, period: LeaderboardPeriod = 'all'): Observable<LeaderboardEntry[]> {
    return this.http.get<LeaderboardEntry[]>(
      `${this.apiUrl}/leaderboard/top?count=${count}&period=${period}`
    );
  }

  // null when the player has no completed game in the period (204)
  getMyBest(playerId: string, period: LeaderboardPeriod = 'all'): Observable<MyBest | null> {
    return this.http.get<MyBest | null>(
      `${this.apiUrl}/leaderboard/me?playerId=${playerId}&period=${period}`
    );
  }

  getConfig(): Observable<GameConfig> {
    return this.http.get<GameConfig>(`${this.apiUrl}/game/config`);
  }

  startSession(playerId?: string, mode: GameMode = 'classic', challengeOf?: string): Observable<StartSessionResponse> {
    return this.http.post<StartSessionResponse>(
      `${this.apiUrl}/game/session/start`,
      { playerId: playerId ?? '00000000-0000-0000-0000-000000000000', mode, challengeOf }
    );
  }

  endSession(sessionId: string): Observable<EndSessionResponse> {
    return this.http.post<EndSessionResponse>(
      `${this.apiUrl}/game/session/end`,
      { sessionId }
    );
  }
}