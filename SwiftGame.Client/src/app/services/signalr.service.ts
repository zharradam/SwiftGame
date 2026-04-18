import { Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject, BehaviorSubject } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

export interface ChatMessage {
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
  private chatMessage$  = new Subject<ChatMessage>();
  private chatHistory$  = new BehaviorSubject<ChatMessage[]>([]);
  private chatError$    = new Subject<string>();

  readonly chatMessage  = this.chatMessage$.asObservable();
  readonly chatHistory  = this.chatHistory$.asObservable();
  readonly chatError    = this.chatError$.asObservable();

  constructor(private auth: AuthService) {
    // ── Leaderboard connection (no auth needed) ───────────────────────────
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

    // ── Chat connection (passes JWT for username resolution) ──────────────
    this.chatConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.hubUrl}/hubs/chat`, {
        accessTokenFactory: () => this.auth.getAccessToken() ?? ''
      })
      .withAutomaticReconnect()
      .build();

    this.chatConnection.on('ReceiveMessage', (message: ChatMessage) => {
      this.chatMessage$.next(message);
    });

    this.chatConnection.on('ChatHistory', (messages: ChatMessage[]) => {
      this.chatHistory$.next(messages);
    });

    this.chatConnection.on('ChatError', (error: string) => {
      this.chatError$.next(error);
    });

    this.chatConnection.start()
      .then(() => console.log('Chat SignalR connected'))
      .catch(err => console.error('Chat SignalR connection failed:', err));

    this.chatConnection.onreconnected(() => console.log('Chat SignalR reconnected'));
    this.chatConnection.onclose(()     => console.log('Chat SignalR closed'));
  }

  sendChatMessage(message: string): void {
    if (this.chatConnection.state === signalR.HubConnectionState.Connected) {
      this.chatConnection.invoke('SendMessage', message)
        .catch(err => console.error('Chat send failed:', err));
    }
  }

  ngOnDestroy(): void {
    this.leaderboardConnection.stop();
    this.chatConnection.stop();
  }

  reconnectChat(): void {
    console.log('Reconnecting chat with token:', this.auth.getAccessToken() ? 'present' : 'empty');
    this.chatConnection.stop().then(() => {
      this.chatConnection.start()
        .then(() => console.log('Chat SignalR reconnected with auth'))
        .catch(err => console.error('Chat reconnect failed:', err));
    });
  }
}