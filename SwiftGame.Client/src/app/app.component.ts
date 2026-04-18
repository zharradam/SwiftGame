// SwiftGame.Client/src/app/app.component.ts

import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { GameComponent } from './components/game/game.component';
import { LeaderboardComponent } from './components/leaderboard/leaderboard.component';
import { AuthModalComponent } from './components/auth-modal/auth-modal.component';
import { AuthService } from './services/auth.service';
import { ChatComponent } from './components/chat/chat.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, GameComponent, LeaderboardComponent, AuthModalComponent, ChatComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = "Isabelle's Taylor Swift Music Quiz";

  showAuthModal = false;
  authModalMode: 'login' | 'register' = 'login';

  constructor(readonly auth: AuthService) {}

  openLogin() {
    this.authModalMode = 'login';
    this.showAuthModal = true;
  }

  openRegister() {
    this.authModalMode = 'register';
    this.showAuthModal = true;
  }
}