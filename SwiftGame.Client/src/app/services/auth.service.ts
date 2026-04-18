import { Injectable, computed, signal, Injector } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap, catchError, of } from 'rxjs';
import { environment } from '../../environments/environment';
import { SignalrService } from './signalr.service';

export interface User {
  id:          string;
  username:    string;
  email:       string;
  isAdmin:     boolean;
  isModerator: boolean;
  isBanned:    boolean;
}

export interface AuthResponse {
  accessToken:       string;
  refreshToken:      string;
  accessTokenExpiry: string;
  user:              User;
}

export interface RegisterRequest {
  username: string;
  email:    string;
  password: string;
}

export interface LoginRequest {
  email:    string;
  password: string;
}

const ACCESS_TOKEN_KEY  = 'swiftgame_access_token';
const REFRESH_TOKEN_KEY = 'swiftgame_refresh_token';
const USER_KEY          = 'swiftgame_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly apiUrl = `${environment.apiUrl}/auth`;

  private _user    = signal<User | null>(this.loadUserFromStorage());
  private _loading = signal(false);
  private _error   = signal<string | null>(null);

  readonly user            = this._user.asReadonly();
  readonly loading         = this._loading.asReadonly();
  readonly error           = this._error.asReadonly();
  readonly isAuthenticated = computed(() => this._user() !== null);
  readonly username        = computed(() => this._user()?.username ?? 'Guest');
  readonly isAdmin         = computed(() => this._user()?.isAdmin ?? false);
  readonly isModerator     = computed(() => this._user()?.isModerator ?? false);
  readonly canModerate     = computed(() => this._user()?.isAdmin || this._user()?.isModerator || false);
  readonly isBanned        = computed(() => this._user()?.isBanned ?? false);

  constructor(
    private http:     HttpClient,
    private router:   Router,
    private injector: Injector
  ) {}

  register(req: RegisterRequest) {
    this._loading.set(true);
    this._error.set(null);

    return this.http.post<AuthResponse>(`${this.apiUrl}/register`, req).pipe(
      tap(res => this.handleAuthSuccess(res)),
      catchError(err => {
        this._error.set(err.error?.message ?? 'Registration failed.');
        this._loading.set(false);
        return of(null);
      })
    );
  }

  login(req: LoginRequest) {
    this._loading.set(true);
    this._error.set(null);

    return this.http.post<AuthResponse>(`${this.apiUrl}/login`, req).pipe(
      tap(res => this.handleAuthSuccess(res)),
      catchError(err => {
        this._error.set(err.error?.message ?? 'Login failed.');
        this._loading.set(false);
        return of(null);
      })
    );
  }

  refresh() {
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
    if (!refreshToken) return of(null);

    return this.http
      .post<AuthResponse>(`${this.apiUrl}/refresh`, { refreshToken })
      .pipe(
        tap(res => this.handleAuthSuccess(res)),
        catchError(() => { this.logout(); return of(null); })
      );
  }

  logout() {
    const token = this.getAccessToken();
    if (token) {
      this.http.post(`${this.apiUrl}/logout`, {}).subscribe();
    }
    this.clearStorage();
    this._user.set(null);
    this.router.navigate(['/']);
  }

  getAccessToken(): string | null {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }
  
  isTokenExpiredSoon(thresholdSeconds = 60): boolean {
    const token = this.getAccessToken();
    if (!token) return true;

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const expiresAt = payload.exp * 1000;
      return Date.now() > expiresAt - thresholdSeconds * 1000;
    } catch {
      return true;
    }
  }

  private handleAuthSuccess(res: AuthResponse | null) {
    if (!res) return;

    // Parse role claims from JWT to ensure they're current
    const payload    = JSON.parse(atob(res.accessToken.split('.')[1]));
    const user: User = {
      id:          res.user.id,
      username:    res.user.username,
      email:       res.user.email,
      isAdmin:     payload['isAdmin']     === 'true',
      isModerator: payload['isModerator'] === 'true',
      isBanned:    payload['isBanned']    === 'true',
    };

    localStorage.setItem(ACCESS_TOKEN_KEY,  res.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, res.refreshToken);
    localStorage.setItem(USER_KEY,          JSON.stringify(user));
    this._user.set(user);
    this._loading.set(false);
    this._error.set(null);

    this.injector.get(SignalrService).reconnectChat();
  }

  private clearStorage() {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  }

  private loadUserFromStorage(): User | null {
    try {
      const raw = localStorage.getItem(USER_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch { return null; }
  }
}