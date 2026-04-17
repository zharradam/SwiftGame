import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface RoundResponse {
  songId: string;
  provider: string;
  previewUrl: string;
  startAt: number;
  choices: string[];
}

export interface SubmitAnswerRequest {
  songId: string;
  provider: string;
  selectedTitle: string;
  responseTimeMs: number;
}

export interface SubmitAnswerResponse {
  isCorrect: boolean;
  pointsEarned: number;
  correctTitle: string;
  responseTimeMs: number;
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
}

export interface StartSessionResponse {
  sessionId: string;
}

export interface EndSessionResponse {
  rank:         number;
  totalPlayers: number;
}

@Injectable({
  providedIn: 'root'
})
export class GameService {
  private readonly apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getRound(): Observable<RoundResponse> {
    return this.http.get<RoundResponse>(`${this.apiUrl}/game/round`);
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

  startSession(): Observable<StartSessionResponse> {
    return this.http.post<StartSessionResponse>(
      `${this.apiUrl}/game/session/start`, {});
  }

  endSession(sessionId: string): Observable<EndSessionResponse> {
    return this.http.post<EndSessionResponse>(
      `${this.apiUrl}/game/session/end`, { sessionId });
  }
}