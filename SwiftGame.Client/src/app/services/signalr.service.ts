import { Injectable, OnDestroy } from '@angular/core';
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

  readonly allMessages  = this.allMessages$.asObservable();
  readonly chatError    = this.chatError$.asObservable();
  readonly playerBanned = this.playerBanned$.asObservable();

  constructor(private auth: AuthService) {
    // ── Leaderboard connection ────────────────────────────────────────────
    this.leaderboardConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.hubUrl}/hubs/leaderboard`)
      .withAutomaticReconnect()
      .build();

    this.leaderboardConnection.on('LeaderboardUpdated', () => {
      this.leaderboardUpdated$.next();
    });

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

    this.chatConnection.on('ReceiveMessage', (message: ChatMessage) => {
      this.allMessages$.next([...this.allMessages$.value, message]);
    });

    this.chatConnection.on('ChatHistory', (messages: ChatMessage[]) => {
      this.allMessages$.next(messages);
    });

    this.chatConnection.on('ChatError', (error: string) => {
      this.chatError$.next(error);
    });

    this.chatConnection.on('MessageDeleted', (messageId: string) => {
      this.allMessages$.next(
        this.allMessages$.value.filter(m => m.id !== messageId)
      );
    });

    this.chatConnection.on('PlayerBanned', (username: string) => {
      // Remove banned player's messages
      this.allMessages$.next(
        this.allMessages$.value.filter(m => m.username !== username)
      );
      this.playerBanned$.next(username);
    });

    this.chatConnection.on('PlayerUnbanned', (_username: string) => {
      // Nothing to do on the message list
    });

    // Delay start to allow auth state to initialise from localStorage
    setTimeout(() => {
      this.chatConnection.start()
        .then(() => console.log('Chat SignalR connected'))
        .catch(err => console.error('Chat SignalR connection failed:', err));
    }, 100);

    this.chatConnection.onreconnected(() => console.log('Chat SignalR reconnected'));
    this.chatConnection.onclose(()     => console.log('Chat SignalR closed'));
  }

  sendChatMessage(message: string): void {
    if (this.chatConnection.state === signalR.HubConnectionState.Connected) {
      this.chatConnection.invoke('SendMessage', message)
        .catch(err => console.error('Chat send failed:', err));
    }
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