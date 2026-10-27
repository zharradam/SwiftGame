import { Injectable, OnDestroy, NgZone } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject, BehaviorSubject } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

export interface ChatMessage {
  id:        string;
  username:  string;
  message:   string;
  timestamp: string;
  isGuest:   boolean;
  isSystem:  boolean;
}

export interface Presence {
  count: number;
  names: string[];
}

export interface ScoreAnnouncement {
  playerName: string;
  points:     number;
  rank:       number;
}

@Injectable({ providedIn: 'root' })
export class SignalrService implements OnDestroy {

  // ── Leaderboard ───────────────────────────────────────────────────────────
  private leaderboardConnection: signalR.HubConnection;
  private leaderboardUpdated$ = new Subject<void>();
  readonly leaderboardUpdated = this.leaderboardUpdated$.asObservable();

  // ── Chat ──────────────────────────────────────────────────────────────────
  private chatConnection: signalR.HubConnection;
  private allMessages$  = new BehaviorSubject<ChatMessage[]>([]);
  private chatError$    = new Subject<string>();
  private playerBanned$ = new Subject<string>();
  private presence$     = new BehaviorSubject<Presence>({ count: 0, names: [] });
  private chatOnline$   = new BehaviorSubject<boolean>(true);
  private scoreAnnounced$ = new Subject<ScoreAnnouncement>();

  readonly allMessages  = this.allMessages$.asObservable();
  readonly chatError    = this.chatError$.asObservable();
  readonly playerBanned = this.playerBanned$.asObservable();
  readonly presence     = this.presence$.asObservable();
  readonly chatOnline   = this.chatOnline$.asObservable();
  readonly scoreAnnounced = this.scoreAnnounced$.asObservable();

  constructor(private auth: AuthService, private zone: NgZone) {
    // ── Leaderboard connection ────────────────────────────────────────────
    this.leaderboardConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.hubUrl}/hubs/leaderboard`)
      .withAutomaticReconnect()
      .build();

    this.leaderboardConnection.on('LeaderboardUpdated', () => this.zone.run(() => {
      this.leaderboardUpdated$.next();
    }));

    this.leaderboardConnection.start()
      .then(() => console.log('Leaderboard SignalR connected'))
      .catch(err => console.error('Leaderboard SignalR connection failed:', err));

    this.leaderboardConnection.onreconnected(() => console.log('Leaderboard SignalR reconnected'));
    this.leaderboardConnection.onclose(()     => console.log('Leaderboard SignalR closed'));

    // ── Chat connection ───────────────────────────────────────────────────
    this.chatConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.hubUrl}/hubs/chat`, {
        accessTokenFactory: () => this.auth.getAccessToken() ?? ''
      })
      .withAutomaticReconnect()
      .build();

    this.chatConnection.on('ReceiveMessage', (message: ChatMessage) => this.zone.run(() => {
      this.allMessages$.next([...this.allMessages$.value, message]);
    }));

    this.chatConnection.on('ChatHistory', (messages: ChatMessage[]) => this.zone.run(() => {
      this.allMessages$.next(messages);
    }));

    this.chatConnection.on('Presence', (p: Presence) => this.zone.run(() => this.presence$.next(p)));
    this.chatConnection.on('ScoreAnnounced', (a: ScoreAnnouncement) => this.zone.run(() => this.scoreAnnounced$.next(a)));

    this.chatConnection.on('ChatError', (error: string) => this.zone.run(() => {
      this.chatError$.next(error);
    }));

    this.chatConnection.on('MessageDeleted', (messageId: string) => this.zone.run(() => {
      this.allMessages$.next(
        this.allMessages$.value.filter(m => m.id !== messageId)
      );
    }));

    this.chatConnection.on('PlayerBanned', (username: string) => this.zone.run(() => {
      // Remove banned player's messages
      this.allMessages$.next(
        this.allMessages$.value.filter(m => m.username !== username)
      );
      this.playerBanned$.next(username);
    }));

    this.chatConnection.on('PlayerUnbanned', (_username: string) => this.zone.run(() => {
      // Nothing to do on the message list
    }));

    // Delay start to allow auth state to initialise from localStorage
    setTimeout(() => {
      this.chatConnection.start()
        .then(() => console.log('Chat SignalR connected'))
        .catch(err => console.error('Chat SignalR connection failed:', err));
    }, 100);

    this.chatConnection.onreconnecting(() => this.zone.run(() => this.chatOnline$.next(false)));
    this.chatConnection.onreconnected(() => this.zone.run(() => this.chatOnline$.next(true)));
    this.chatConnection.onclose(()     => this.zone.run(() => this.chatOnline$.next(false)));
  }

  // Resolves true once the server accepted the message
  sendChatMessage(message: string): Promise<boolean> {
    if (this.chatConnection.state !== signalR.HubConnectionState.Connected) return Promise.resolve(false);
    return this.chatConnection.invoke('SendMessage', message).then(() => true, () => false);
  }

  deleteMessage(messageId: string): void {
    if (this.chatConnection.state === signalR.HubConnectionState.Connected) {
      this.chatConnection.invoke('DeleteMessage', messageId)
        .catch(err => console.error('Delete failed:', err));
    }
  }

  reconnectChat(): void {
    this.chatConnection.stop().then(() => {
      this.chatConnection.start()
        .then(() => console.log('Chat SignalR reconnected with auth'))
        .catch(err => console.error('Chat reconnect failed:', err));
    });
  }

  ngOnDestroy(): void {
    this.leaderboardConnection.stop();
    this.chatConnection.stop();
  }
}