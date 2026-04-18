import { Component, OnDestroy, OnInit, ViewChild, ElementRef, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { GameService, RoundResponse, SubmitAnswerResponse } from '../../services/game.service';
import { AuthService } from '../../services/auth.service';

type GameState = 'loading' | 'playing' | 'answered' | 'error';

@Component({
  selector: 'swiftgame-game',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './game.component.html',
  styleUrl: './game.component.scss'
})
export class GameComponent implements OnInit, OnDestroy {
  state:             GameState = 'loading';
  gameStarted:       boolean   = false;
  round:             RoundResponse | null      = null;
  result:            SubmitAnswerResponse | null = null;
  selectedChoice:    string | null = null;
  errorMessage:      string   = '';
  sessionId:         string   = '';
  imageBaseUrl:      string   = '';
  imageCount:        number   = 15;
  currentImageUrl:   string   = '';
  questionsPerGame:  number   = 10;
  currentQuestion:   number   = 0;
  questionsAnswered: number   = 0;
  totalScore:        number   = 0;
  correctCount:      number   = 0;
  gameOver:          boolean  = false;
  countdown:         number   = 0;
  isCountingDown:    boolean  = false;
  playerRank:        number   = 0;
  totalPlayers:      number   = 0;
  countdownKey:      number   = 0;
  readonly gameTitle: string = 'Isabelle\'s Taylor Swift Music Quiz';

  @Output() openLogin    = new EventEmitter<void>();
  @Output() openRegister = new EventEmitter<void>();

  private imageQueue: number[] = [];
  private usedSongIds: string[] = [];
  private fireworksAnimationId: number = 0;
  private countdownInterval: ReturnType<typeof setInterval> | null = null;
  private timerInterval:     ReturnType<typeof setInterval> | null = null;
  private startTime:         number = 0;

  elapsedMs: number = 0;
  audio:     HTMLAudioElement | null = null;

  @ViewChild('fireworksCanvas') fireworksCanvas!: ElementRef<HTMLCanvasElement>;

  constructor(private gameService: GameService, public auth: AuthService) {}

  ngOnInit(): void {
    this.gameService.getConfig().subscribe({
      next: (config) => {
        this.questionsPerGame = config.questionsPerGame;
        this.imageBaseUrl     = config.imageBaseUrl;
        this.imageCount       = config.imageCount;
      },
      error: () => {}
    });
  }

  ngOnDestroy(): void {
    if (this.fireworksAnimationId) {
      cancelAnimationFrame(this.fireworksAnimationId);
    }
    this.stopTimer();
    this.stopAudio();
    this.stopCountdown();
  }



onLoginClick()  { this.openLogin.emit(); }
onSignUpClick() { this.openRegister.emit(); }

  get playerId(): string {
    return this.auth.user()?.id ?? '00000000-0000-0000-0000-000000000000';
  }

  loadRound(): void {
    this.stopCountdown();
    this.state          = 'loading';
    this.round          = null;
    this.result         = null;
    this.selectedChoice = null;
    this.elapsedMs      = 0;
    this.stopAudio();

    this.gameService.getRound(this.usedSongIds).subscribe({
      next:  (round) => this.startPlaying(round),
      error: (err)   => {
        this.state        = 'error';
        this.errorMessage = 'Failed to load round. Please try again.';
        console.error(err);
      }
    });
  }

  private startPlaying(round: RoundResponse): void {
    this.usedSongIds.push(round.songDbId);
    this.currentQuestion++;
    this.round          = round;
    this.result         = null;
    this.selectedChoice = null;
    this.elapsedMs      = 0;
    this.state          = 'playing';
    this.pickRandomImage();
    this.startAudio(round.previewUrl, round.startAt);
    this.startTimer();
  }

  selectChoice(choice: string): void {
    if (this.state !== 'playing' || !this.round) return;

    this.stopTimer();
    this.stopAudio();
    this.selectedChoice = choice;
    this.state          = 'answered';

    this.gameService.submitAnswer({
      songId:         this.round.songId,
      provider:       this.round.provider,
      selectedTitle:  choice,
      responseTimeMs: this.elapsedMs,
      sessionId:      this.sessionId,
      playerId:       this.playerId
    }).subscribe({
      next: (result) => {
        this.result      = result;
        this.totalScore += result.pointsEarned;
        this.questionsAnswered++;

        if (result.isCorrect) this.correctCount++;

        if (this.questionsAnswered >= this.questionsPerGame) {
          // End session and show game over
          this.gameService.endSession(this.sessionId).subscribe({
            next: (response) => {
              this.playerRank   = response.rank;
              this.totalPlayers = response.totalPlayers;
            },
            error: (err) => console.error(err)
          });
          setTimeout(() => {
            this.gameOver = true;
            if (this.showFireworks) {
              setTimeout(() => this.launchFireworks(), 100);
            }
          }, 2000);
        } else {
          // Auto countdown to next question
          this.startCountdown(3, () => this.loadRound());
        }
      },
      error: (err) => console.error(err)
    });
  }

  getChoiceClass(choice: string): string {
    if (this.state !== 'answered' || !this.result) return '';
    if (choice === this.result.correctTitle) return 'correct';
    if (choice === this.selectedChoice)      return 'incorrect';
    return '';
  }

  formatTime(ms: number): string {
    const seconds    = Math.floor(ms / 1000);
    const hundredths = Math.floor((ms % 1000) / 10);
    return `${seconds}.${hundredths.toString().padStart(2, '0')}s`;
  }

  startGame(): void {
    this.usedSongIds = [];
    this.gameStarted = true;
    this.gameService.startSession(this.playerId).subscribe({
      next: (response) => {
        this.sessionId = response.sessionId;
        this.buildImageQueue();
        this.loadRound();
      },
      error: (err) => {
        console.error('Failed to start session:', err);
        this.gameStarted = false;  // drop back to home screen rather than starting a broken game
      }
    });
  }

  restartGame(): void {
    this.usedSongIds = [];
    if (this.fireworksAnimationId) {
      cancelAnimationFrame(this.fireworksAnimationId);
      this.fireworksAnimationId = 0;
    }
    this.currentQuestion   = 0;
    this.questionsAnswered = 0;
    this.totalScore        = 0;
    this.correctCount      = 0;
    this.gameOver          = false;
    this.playerRank        = 0;
    this.totalPlayers      = 0;
    this.stopCountdown();

    this.gameService.startSession(this.playerId).subscribe({
      next: (response) => {
        this.sessionId = response.sessionId;
        this.buildImageQueue();
        this.loadRound();
      },
      error: () => {
        this.buildImageQueue();
        this.loadRound();
      }
    });
  }

  goHome(): void {
    if (this.fireworksAnimationId) {
      cancelAnimationFrame(this.fireworksAnimationId);
      this.fireworksAnimationId = 0;
    }
    this.currentQuestion   = 0;
    this.questionsAnswered = 0;
    this.totalScore        = 0;
    this.correctCount      = 0;
    this.gameOver          = false;
    this.gameStarted       = false;
    this.playerRank        = 0;
    this.totalPlayers      = 0;
    this.state             = 'loading';
    this.stopCountdown();
    this.stopAudio();
    this.stopTimer();
  }

  get displayQuestion(): number {
    return Math.min(this.currentQuestion, this.questionsPerGame);
  }

  get rankMessage(): string {
    if (this.playerRank === 1)  return '👑 NEW HIGH SCORE!';
    if (this.playerRank <= 3)   return '🏆 TOP 3 ALL TIME!';
    if (this.playerRank <= 10)  return '⭐ TOP 10 ALL TIME!';
    if (this.playerRank <= 50)  return '🎵 TOP 50 ALL TIME!';
    return '🎤 GREAT EFFORT!';
  }

  get showFireworks(): boolean {
    return this.playerRank > 0 && this.playerRank <= 10;
  }

  get accuracyPercent(): number {
    if (this.questionsAnswered === 0) return 0;
    return Math.round((this.correctCount / this.questionsAnswered) * 100);
  }

  // ── Audio ──────────────────────────────────────────────────────────────────

  private startAudio(url: string, startAt: number): void {
    this.audio = new Audio(url);
    this.audio.currentTime = startAt;
    this.audio.play().catch(err => console.error('Audio play failed:', err));
  }

  private stopAudio(): void {
    if (this.audio) {
      this.audio.pause();
      this.audio.src = '';
      this.audio     = null;
    }
  }

  // ── Timer ──────────────────────────────────────────────────────────────────

  private startTimer(): void {
    this.startTime     = Date.now();
    this.timerInterval = setInterval(() => {
      this.elapsedMs = Date.now() - this.startTime;

      // Auto-timeout after 10 seconds
      if (this.elapsedMs >= 10000 && this.state === 'playing') {
        this.stopTimer();
        this.selectChoice('__timeout__');
      }
    }, 10);
  }

  private stopTimer(): void {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
      this.timerInterval = null;
    }
  }

  // ── Countdown ──────────────────────────────────────────────────────────────

  private startCountdown(seconds: number, onComplete: () => void): void {
    this.isCountingDown = true;
    this.countdown      = seconds;
    this.countdownKey++;

    this.countdownInterval = setInterval(() => {
      this.countdown--;
      this.countdownKey++;
      if (this.countdown <= 0) {
        this.stopCountdown();
        onComplete();
      }
    }, 1000);
  }

  private stopCountdown(): void {
    if (this.countdownInterval) {
      clearInterval(this.countdownInterval);
      this.countdownInterval = null;
    }
    this.isCountingDown = false;
    this.countdown      = 0;
  }

  // ── Images ─────────────────────────────────────────────────────────────────

  private pickRandomImage(): void {
    if (!this.imageBaseUrl || this.imageQueue.length === 0) return;
    const index          = this.imageQueue[this.currentQuestion - 1] ?? 1;
    const padded         = index.toString().padStart(2, '0');
    this.currentImageUrl = `${this.imageBaseUrl}/image-${padded}.jpeg`;
  }

  // ── Fireworks ──────────────────────────────────────────────────────────────

  private launchFireworks(): void {
    const canvas = this.fireworksCanvas?.nativeElement;
    if (!canvas) return;

    const ctx     = canvas.getContext('2d')!;
    canvas.width  = canvas.offsetWidth;
    canvas.height = canvas.offsetHeight;

    const particles: any[] = [];
    const colors = ['#ff69b4', '#ffd700', '#c850c0', '#8a2be2', '#ff6363', '#48c774'];

    const createBurst = (x: number, y: number) => {
      for (let i = 0; i < 80; i++) {
        const angle = (Math.PI * 2 / 80) * i;
        const speed = Math.random() * 6 + 2;
        particles.push({
          x, y,
          vx:    Math.cos(angle) * speed,
          vy:    Math.sin(angle) * speed,
          alpha: 1,
          color: colors[Math.floor(Math.random() * colors.length)],
          size:  Math.random() * 3 + 1,
          decay: Math.random() * 0.02 + 0.01
        });
      }
    };

    let frame              = 0;
    let animationId:number = 0;
    this.fireworksAnimationId = 0;

    const animate = () => {
      ctx.clearRect(0, 0, canvas.width, canvas.height);

      if (frame % 40 === 0) {
        createBurst(
          Math.random() * canvas.width,
          Math.random() * canvas.height * 0.6
        );
      }

      particles.forEach(p => {
        p.x     += p.vx;
        p.y     += p.vy;
        p.vy    += 0.1;
        p.alpha -= p.decay;

        ctx.save();
        ctx.globalAlpha = Math.max(0, p.alpha);
        ctx.fillStyle   = p.color;
        ctx.beginPath();
        ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
      });

      for (let i = particles.length - 1; i >= 0; i--) {
        if (particles[i].alpha <= 0) particles.splice(i, 1);
      }

      frame++;
      this.fireworksAnimationId = requestAnimationFrame(animate);
    };

    animate();
  }

  private buildImageQueue(): void {
    // Create array of indices 1..imageCount, shuffle it, take first questionsPerGame
    this.imageQueue = Array.from({ length: this.imageCount }, (_, i) => i + 1)
      .sort(() => Math.random() - 0.5)
      .slice(0, this.questionsPerGame);
  }
}