import { Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class SignalrService implements OnDestroy {
  private connection: signalR.HubConnection;
  private leaderboardUpdated$ = new Subject<void>();

  readonly leaderboardUpdated = this.leaderboardUpdated$.asObservable();

  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.hubUrl}/hubs/leaderboard`)
      .withAutomaticReconnect()
      .build();

    this.connection.on('LeaderboardUpdated', () => {
      this.leaderboardUpdated$.next();
    });

    this.connection.start()
      .then(() => console.log('SignalR connected'))
      .catch(err => console.error('SignalR connection failed:', err));

    this.connection.onreconnected(() => console.log('SignalR reconnected'));
    this.connection.onclose(() => console.log('SignalR connection closed'));
  }

  ngOnDestroy(): void {
    this.connection.stop();
  }
}