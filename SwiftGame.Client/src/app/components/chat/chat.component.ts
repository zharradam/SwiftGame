import { Component, OnInit, OnDestroy, AfterViewChecked, ViewChild, ElementRef, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { SignalrService, ChatMessage, Presence, ScoreAnnouncement } from '../../services/signalr.service';
import { AuthService } from '../../services/auth.service';
import { AdminPanelComponent } from '../admin-panel/admin-panel.component';

@Component({
  selector: 'swiftgame-chat',
  standalone: true,
  imports: [CommonModule, FormsModule, AdminPanelComponent],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.scss'
})
export class ChatComponent implements OnInit, OnDestroy, AfterViewChecked {
  messages:      ChatMessage[] = [];
  inputText:     string        = '';
  errorMessage:  string        = '';
  showAdminPanel: boolean      = false;
  presence:      Presence      = { count: 0, names: [] };
  online:        boolean       = true;
  toast:         ScoreAnnouncement | null = null;
  private toastTimer: ReturnType<typeof setTimeout> | null = null;
  private shouldScroll  = false;
  private subscriptions: Subscription[] = [];

  @ViewChild('messageList')  messageList!:  ElementRef<HTMLDivElement>;
  @ViewChild('scrollAnchor') scrollAnchor!: ElementRef<HTMLDivElement>;
  @Output() openLogin = new EventEmitter<void>();

  constructor(readonly auth: AuthService, private signalr: SignalrService) {}

  ngOnInit(): void {
    this.subscriptions.push(
      this.signalr.allMessages.subscribe(messages => {
        this.messages    = messages.filter(m => !m.isSystem);
        this.shouldScroll = true;
      })
    );

    this.subscriptions.push(
      this.signalr.presence.subscribe(p => this.presence = p),
      this.signalr.chatOnline.subscribe(o => this.online = o),
      this.signalr.scoreAnnounced.subscribe(a => {
        this.toast = a;
        if (this.toastTimer) clearTimeout(this.toastTimer);
        this.toastTimer = setTimeout(() => this.toast = null, 6000);
      })
    );

    this.subscriptions.push(
      this.signalr.chatError.subscribe(error => {
        this.errorMessage = error;
        setTimeout(() => this.errorMessage = '', 3000);
      })
    );
  }

  ngAfterViewChecked(): void {
    if (this.shouldScroll) {
      this.scrollToBottom();
      this.shouldScroll = false;
    }
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach(s => s.unsubscribe());
    if (this.toastTimer) clearTimeout(this.toastTimer);
  }

  get chatCount(): number {
    return this.messages.filter(m => !m.isSystem).length;
  }

  get presenceLabel(): string {
    const shown = this.presence.names.slice(0, 3).join(', ');
    const more  = this.presence.names.length - 3;
    return shown ? (more > 0 ? `${shown} +${more}` : shown) : '';
  }

  send(): void {
    const text = this.inputText.trim();
    if (!text || !this.auth.isAuthenticated()) return;
    this.signalr.sendChatMessage(text).then(ok => {
      if (ok) { this.inputText = ''; this.shouldScroll = true; }
      else    { this.errorMessage = "Message not sent. Chat is reconnecting, so try again in a moment."; setTimeout(() => this.errorMessage = '', 4000); }
    });
  }

  deleteMessage(messageId: string): void {
    this.signalr.deleteMessage(messageId);
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  private scrollToBottom(): void {
    this.scrollAnchor?.nativeElement?.scrollIntoView({ behavior: 'instant' });
  }

  formatTime(timestamp: string): string {
    try {
      return new Date(timestamp).toLocaleTimeString([], { 
        hour: '2-digit', 
        minute: '2-digit' 
      });
    } catch {
      return timestamp;
    }
  }
}