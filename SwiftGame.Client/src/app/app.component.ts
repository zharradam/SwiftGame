import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { GameComponent } from './components/game/game.component';
import { LeaderboardComponent } from './components/leaderboard/leaderboard.component';
import { AuthModalComponent } from './components/auth-modal/auth-modal.component';
import { ChatComponent } from './components/chat/chat.component';
import { AuthService } from './services/auth.service';

type Tab = 'game' | 'leaderboard' | 'chat';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, GameComponent, LeaderboardComponent, AuthModalComponent, ChatComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = "Swiftology";

  showAuthModal = false;
  authModalMode: 'login' | 'register' = 'login';
  activeTab: Tab = 'game';

  constructor(readonly auth: AuthService) {}

  openLogin() {
    this.authModalMode = 'login';
    this.showAuthModal = true;
  }

  openRegister() {
    this.authModalMode = 'register';
    this.showAuthModal = true;
  }

  setTab(tab: Tab) {
    this.activeTab = tab;
  }
}