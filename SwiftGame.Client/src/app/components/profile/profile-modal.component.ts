import { Component, EventEmitter, HostListener, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { GameService, Progress } from '../../services/game.service';
import { AuthService } from '../../services/auth.service';
import { SkinService, titleFor } from '../../services/skin.service';
import { ALL_ERAS, eraByKey } from '../game/eras';

@Component({
  selector: 'swiftgame-profile',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './profile-modal.component.html',
  styleUrl: './profile-modal.component.scss'
})
export class ProfileModalComponent implements OnInit {
  @Output() close = new EventEmitter<void>();

  readonly eras = ALL_ERAS;
  avatars: string[] = [];
  progress: Progress | null = null;
  avatar: string | null = null;
  era: string | null = null;
  saving = false;
  error = '';

  constructor(private game: GameService, readonly auth: AuthService, private skin: SkinService) {}

  ngOnInit(): void {
    const u = this.auth.user();
    this.avatar = u?.avatar ?? null;
    this.era = u?.favouriteEra ?? null;
    this.game.getAvatars().subscribe({ next: r => this.avatars = r.avatars, error: () => {} });
    if (u) this.game.getProgress(u.id).subscribe({
      next: p => { this.progress = p; this.avatar = p.avatar; this.era = p.favouriteEra; },
      error: () => this.error = "Couldn't load your progress. Try again in a moment."
    });
  }

  @HostListener('document:keydown.escape')
  cancel(): void {
    this.skin.apply(this.auth.user()?.favouriteEra ?? null);   // undo the live preview
    this.close.emit();
  }

  pickEra(key: string | null): void {
    this.era = key;
    this.skin.apply(key);       // preview the skin straight away
  }

  save(): void {
    this.saving = true;
    this.game.updateProfile(this.avatar, this.era).subscribe({
      next: r => {
        this.auth.updateUser({ avatar: r.avatar, favouriteEra: r.favouriteEra });
        this.saving = false;
        this.close.emit();
      },
      error: () => { this.saving = false; this.error = "Couldn't save. Try again in a moment."; }
    });
  }

  get level() { return titleFor(this.progress?.totalPoints ?? 0); }
  eraColor(key: string): string { return eraByKey(key)?.color ?? '#ff69b4'; }
  get goldList(): number[] { return Array.from({ length: Math.min(12, this.progress?.goldBeads ?? 0) }, (_, i) => i); }

  short(key: string): string {
    return ({ ttpd: 'TTPD', showgirl: 'SG', midnights: 'MN', evermore: 'ev', folklore: 'fo', lover: 'Lo',
              rep: 'rep', '1989': '89', red: 'Red', speaknow: 'SN', fearless: 'Fe', debut: 'TS' } as Record<string, string>)[key] ?? key;
  }

  get beadsEarned(): number { return this.progress?.beads.filter(b => b.earned).length ?? 0; }
}
