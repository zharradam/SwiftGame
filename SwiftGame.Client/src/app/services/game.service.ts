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
}

export interface LeaderboardEntry {
  playerName:    string;
  songTitle:     string;
  pointsEarned:  number;
  responseTimeMs: number;
  playedAt:      string;
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
}

export interface StartSessionResponse {
  sessionId: string;
  playerId:  string;
}

export interface EndSessionResponse {
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

  getRound(excludeIds: string[] = []): Observable<RoundResponse> {
    const params = excludeIds.length 
      ? `?excludeIds=${excludeIds.join(',')}` 
      : '';
    return this.http.get<RoundResponse>(`${this.apiUrl}/game/round${params}`);
  }

  submitAnswer(request: SubmitAnswerRequest): Observable<SubmitAnswerResponse> {
    return this.http.post<SubmitAnswerResponse>(
      `${this.apiUrl}/game/submit`,
      request
    );
  }

  getLeaderboard(count: number = 10): Observable<LeaderboardEntry[]> {
    return this.http.get<LeaderboardEntry[]>(
      `${this.apiUrl}/leaderboard/top?count=${count}`
    );
  }

  getConfig(): Observable<GameConfig> {
    return this.http.get<GameConfig>(`${this.apiUrl}/game/config`);
  }

  startSession(playerId?: string): Observable<StartSessionResponse> {
    return this.http.post<StartSessionResponse>(
      `${this.apiUrl}/game/session/start`,
      { playerId: playerId ?? '00000000-0000-0000-0000-000000000000' }
    );
  }

  endSession(sessionId: string): Observable<EndSessionResponse> {
    return this.http.post<EndSessionResponse>(
      `${this.apiUrl}/game/session/end`,
      { sessionId }
    );
  }
}