import { Component, OnInit, OnDestroy, AfterViewChecked, ViewChild, ElementRef, Output, EventEmitter } from '@angular/core';
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
  messages:     ChatMessage[] = [];
  inputText:    string        = '';
  errorMessage: string        = '';
  private shouldScroll  = false;
  private subscriptions: Subscription[] = [];

  @ViewChild('messageList') messageList!: ElementRef<HTMLDivElement>;
  @ViewChild('scrollAnchor') scrollAnchor!: ElementRef<HTMLDivElement>;
  @Output() openLogin = new EventEmitter<void>();

  constructor(readonly auth: AuthService, private signalr: SignalrService) {}

  ngOnInit(): void {
    this.subscriptions.push(
      this.signalr.allMessages.subscribe(messages => {
        this.messages     = messages;
        this.shouldScroll = true;   // 👈 just set the flag
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
  }

  send(): void {
    const text = this.inputText.trim();
    if (!text || !this.auth.isAuthenticated()) return;
    this.signalr.sendChatMessage(text);
    this.inputText    = '';
    this.shouldScroll = true;  // 👈 trigger scroll on send too
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
}