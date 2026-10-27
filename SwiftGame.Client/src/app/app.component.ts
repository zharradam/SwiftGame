import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { GameComponent } from './components/game/game.component';
import { LeaderboardComponent } from './components/leaderboard/leaderboard.component';
import { AuthModalComponent } from './components/auth-modal/auth-modal.component';
import { ChatComponent } from './components/chat/chat.component';
import { AuthService } from './services/auth.service';
import { VisitService } from './services/visit.service';
import { SkinService } from './services/skin.service';
import { SignalrService } from './services/signalr.service';
import { ProfileModalComponent } from './components/profile/profile-modal.component';

type Tab = 'game' | 'leaderboard' | 'chat';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, GameComponent, LeaderboardComponent, AuthModalComponent, ChatComponent, ProfileModalComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = "Swiftology";

  showAuthModal = false;
  showProfile   = false;
  reactionToast = '';
  private reactionTimer: ReturnType<typeof setTimeout> | null = null;
  authModalMode: 'login' | 'register' = 'login';
  activeTab: Tab = 'game';
  // Phone layout shows one panel at a time; wider layouts always show the game.
  private phone = signal(window.matchMedia('(max-width: 767px)').matches);
  get gameHidden(): boolean { return this.phone() && this.activeTab !== 'game'; }

  constructor(readonly auth: AuthService, visits: VisitService, _skin: SkinService, signalr: SignalrService) {
    visits.recordVisit();
    // Someone reacted to your score
    signalr.reactions.subscribe(r => {
      if (!auth.isAuthenticated() || r.to !== auth.username()) return;
      this.reactionToast = `${r.from} sent you ${r.emoji}`;
      if (this.reactionTimer) clearTimeout(this.reactionTimer);
      this.reactionTimer = setTimeout(() => this.reactionToast = '', 4000);
    });
    window.matchMedia('(max-width: 767px)').addEventListener('change', e => this.phone.set(e.matches));
  }

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