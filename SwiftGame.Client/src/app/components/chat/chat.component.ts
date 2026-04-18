import { Component, OnInit, OnDestroy, ViewChild, ElementRef, AfterViewChecked, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { SignalrService, ChatMessage } from '../../services/signalr.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'swiftgame-chat',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.scss'
})
export class ChatComponent implements OnInit, OnDestroy, AfterViewChecked {
  messages:    ChatMessage[] = [];
  inputText:   string        = '';
  errorMessage: string       = '';
  private shouldScroll = false;
  private subscriptions: Subscription[] = [];

  @ViewChild('messageList') messageList!: ElementRef<HTMLDivElement>;
  @Output() openLogin = new EventEmitter<void>();

  constructor(
    readonly auth: AuthService,
    private signalr: SignalrService
  ) {}

  ngOnInit(): void {
    // Load history
    this.subscriptions.push(
      this.signalr.chatHistory.subscribe(history => {
        this.messages    = history;
        this.shouldScroll = true;
      })
    );

    // Live messages
    this.subscriptions.push(
      this.signalr.chatMessage.subscribe(message => {
        this.messages.push(message);
        this.shouldScroll = true;
      })
    );

    // Errors
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
  }

  send(): void {
    const text = this.inputText.trim();
    if (!text || !this.auth.isAuthenticated()) return;

    this.signalr.sendChatMessage(text);
    this.inputText = '';
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  private scrollToBottom(): void {
    try {
      const el = this.messageList?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    } catch {}
  }
}