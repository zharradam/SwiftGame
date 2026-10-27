import {
  Component,
  HostListener,
  EventEmitter,
  Input,
  OnInit,
  Output
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-auth-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './auth-modal.component.html',
  styleUrl: './auth-modal.component.scss'
})
export class AuthModalComponent implements OnInit {
  @Input() initialMode: 'login' | 'register' = 'login';
  @Output() close = new EventEmitter<void>();
  @Output() authenticated = new EventEmitter<void>();

  mode: 'login' | 'register' = 'login';
  username = '';
  email = '';
  password = '';

  constructor(readonly auth: AuthService) {}

  ngOnInit() {
    this.mode = this.initialMode;
    this.auth.clearError();
    setTimeout(() => (document.querySelector('.auth-form input') as HTMLInputElement | null)?.focus());
  }

  submit() {
    if (this.mode === 'login') {
      this.auth.login({ email: this.email, password: this.password })
        .subscribe(res => {
          if (res) {
            this.authenticated.emit();
            this.close.emit();
          }
        });
    } else {
      this.auth.register({ username: this.username, email: this.email, password: this.password })
        .subscribe(res => {
          if (res) {
            this.authenticated.emit();
            this.close.emit();
          }
        });
    }
  }

  clearError() {
    this.auth.clearError();
  }

  @HostListener('document:keydown.escape')
  onEscape() { this.close.emit(); }

  onOverlayClick(event: MouseEvent) {
    if ((event.target as HTMLElement).classList.contains('auth-overlay')) {
      this.close.emit();
    }
  }
}