import { Component, OnDestroy, OnInit, ViewChild, ElementRef, Output, EventEmitter, Input, HostListener, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { GameService, RoundResponse, SubmitAnswerResponse, GameMode, SongSet, AlbumOption, Progress } from '../../services/game.service';
import { of, Observable } from 'rxjs';
import { drawStory } from './story';
import { ALL_ERAS } from './eras';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../services/auth.service';
import { eraFor, artworkLarge, Era } from './eras';

type GameState = 'loading' | 'playing' | 'answered' | 'error' | 'countdown';

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
  imageCount:        number   = 14;
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
  roundResults:      SubmitAnswerResponse[] = [];
  streak:            number   = 0;
  previousBest:      number | null = null;   // best score before this game, logged-in players only
  shareState:        'idle' | 'copied' | 'shared' = 'idle';

  // ── Modes ──
  mode:          GameMode = 'classic';
  songSet:       string[] | null = null;     // daily / challenge: the fixed songs, in order
  private setIndex = 0;
  dailyDate      = '';
  challenge:     SongSet | null = null;      // set when the page was opened from a challenge link
  albums:        AlbumOption[] = [];
  pickedEras     = new Set<string>();
  showAlbumPicker = false;
  progressBefore: Progress | null = null;
  progressAfter:  Progress | null = null;
  easterEgg      = '';
  storyState:    'idle' | 'working' | 'done' | 'failed' = 'idle';
  readonly eraList = ALL_ERAS;
  readonly gameTitle: string = 'Swiftology';

  // True when the phone layout is showing another tab: pause the round instead of letting it time out unseen
  @Input() set hidden(v: boolean) { this.panelHidden = v; this.syncPause(); }
  private panelHidden = false;
  paused       = false;
  needsTap     = false;          // autoplay was blocked: player must tap to hear the clip
  announcement = '';             // screen-reader text for round events
  private playingTimeout: ReturnType<typeof setTimeout> | null = null;

  @Output() openLogin    = new EventEmitter<void>();
  @Output() openRegister = new EventEmitter<void>();

  private imageQueue: number[] = [];
  private usedSongIds: string[] = [];
  private fireworksAnimationId: number = 0;
  private countdownInterval: ReturnType<typeof setInterval> | null = null;
  private timerInterval:     ReturnType<typeof setInterval> | null = null;
  private startTime:         number = 0;

  private audioContext:  AudioContext | null = null;
  private analyser:      AnalyserNode | null = null;
  private audioSource:   MediaElementAudioSourceNode | null = null;
  private animFrameId:   number = 0;

  barHeights: number[] = new Array(28).fill(0);  // drives the template

  elapsedMs: number = 0;
  audio:     HTMLAudioElement | null = null;

  @ViewChild('fireworksCanvas') fireworksCanvas!: ElementRef<HTMLCanvasElement>;

  constructor(private gameService: GameService, public auth: AuthService) {
    // logging in or out changes the daily lock and bead progress
    effect(() => { this.auth.user(); this.refreshProgress(); });
  }

  ngOnInit(): void {
    const challengeId = new URLSearchParams(location.search).get('challenge');
    if (challengeId) {
      this.gameService.getSet('challenge', challengeId).subscribe({
        next:  set => this.challenge = set,
        error: () => this.startError = "That challenge link has expired, but you can still play a normal game."
      });
      history.replaceState(null, '', location.pathname);   // a refresh shouldn't re-open the challenge
    }
    this.refreshProgress();

    this.gameService.getConfig().subscribe({
      next: (config) => {
        this.questionsPerGame = config.questionsPerGame;
        this.imageBaseUrl     = config.imageBaseUrl;
        this.imageCount       = config.imageCount;
      },
      error: () => {}
    });
  }

  clipFailed = false;

  tapToPlay(): void {
    this.ensureAudioContext();
    this.audio?.play().catch(() => this.clipFailed = true);
  }

  skipClip(): void {
    this.clipFailed = false;
    this.loadRound();   // unanswered: the counters only move on answered rounds
  }

  private clearPlayingTimeout(): void {
    if (this.playingTimeout) { clearTimeout(this.playingTimeout); this.playingTimeout = null; }
  }

  // ── Pause when the game can't be seen ───────────────────────────────────────

  @HostListener('document:visibilitychange')
  syncPause(): void {
    const shouldPause = this.panelHidden || document.hidden;
    const active = this.state === 'playing' || this.isCountingDown;
    if (shouldPause && active && !this.paused) {
      this.paused = true;
      this.stopTimer();
      this.audio?.pause();
      if (this.countdownInterval) { clearInterval(this.countdownInterval); this.countdownInterval = null; }
    } else if (!shouldPause && this.paused) {
      this.paused = false;
      if (this.isCountingDown && this.countdownDone) {
        this.startCountdown(Math.max(this.countdown, 1), this.countdownDone);
      } else if (this.state === 'playing') {
        this.audio?.play().catch(() => this.needsTap = true);
        if (this.elapsedMs > 0) this.startTimer(this.elapsedMs);
      }
    }
  }

  // ── Keyboard: 1-4 answer, Enter plays again ────────────────────────────────

  @HostListener('document:keydown', ['$event'])
  onKey(e: KeyboardEvent): void {
    const t = e.target as HTMLElement;
    if (t?.closest('input, textarea, [contenteditable], [role="dialog"]')) return;
    if (e.key === 'Enter' && this.gameOver && !t?.closest('button')) { this.restartGame(); return; }
    const n = Number(e.key);
    if (this.state === 'playing' && this.round && n >= 1 && n <= this.round.choices.length) {
      this.selectChoice(this.round.choices[n - 1]);
    }
  }

  ngOnDestroy(): void {
    if (this.fireworksAnimationId) {
      cancelAnimationFrame(this.fireworksAnimationId);
    }
    this.stopTimer();
    this.stopAudio();
    this.stopCountdown();
    this.audioContext?.close();
  }



onLoginClick()  { this.openLogin.emit(); }
onSignUpClick() { this.openRegister.emit(); }

  get playerId(): string {
    return this.auth.user()?.id ?? '00000000-0000-0000-0000-000000000000';
  }

  quitGame(): void {
    if (this.questionsAnswered > 0 && !confirm('Quit this game? Your score so far won\'t be saved.')) return;
    this.goHome();
  }

  continueAfterError(): void {
    if (this.questionsAnswered >= this.questionsPerGame) { this.goHome(); return; }
    this.loadRound();
  }

  loadRound(): void {
    this.stopCountdown();
    this.state          = 'loading';
    this.round          = null;
    this.result         = null;
    this.selectedChoice = null;
    this.elapsedMs      = 0;
    this.stopAudio();

    this.fetchRound().subscribe({
      next:  (round) => this.startPlaying(round),
      error: (err) => {
        this.state        = 'error';
        this.errorMessage = this.describe(err);
      }
    });
  }

  private fetchRound(): Observable<RoundResponse> {
    const songId = this.songSet?.[this.setIndex];
    return this.gameService.getRound(this.usedSongIds, {
      songId,
      albums: this.mode === 'album' ? this.selectedAlbumIds : undefined,
      kind:   this.mode === 'eras' ? 'era' : undefined
    });
  }

  private startPlaying(round: RoundResponse): void {
    if (this.songSet) this.setIndex++;
    this.usedSongIds.push(round.songDbId);
    this.currentQuestion = this.questionsAnswered + 1;
    this.round          = round;
    this.result         = null;
    this.selectedChoice = null;
    this.elapsedMs      = 0;
    this.state          = 'playing';
    this.pickRandomImage();
    this.startAudio(round.previewUrl, round.startAt);
    this.syncPause();   // a round that starts while the game is hidden begins paused
  }

  selectChoice(choice: string): void {
    if (this.needsTap && this.state === 'playing') { this.tapToPlay(); return; }
    if (this.state !== 'playing' || !this.round || this.paused || !this.timerInterval && choice !== '__timeout__') return;

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
        this.roundResults.push(result);
        this.streak = result.isCorrect ? this.streak + 1 : 0;

        if (result.isCorrect) this.correctCount++;
        this.announcement = result.isCorrect
          ? `Correct. ${result.correctTitle}. Plus ${result.pointsEarned} points.`
          : `${this.resultStatus}. It was ${result.correctTitle}.`;

        if (this.questionsAnswered >= this.questionsPerGame) {
          // End session and show game over
          let shown = false;
          const show = () => {
            if (shown) return;
            shown = true;
            this.gameOver = true;
            this.announcement = `Game over. ${this.totalScore} points, ${this.correctCount} correct.`;
            this.easterEgg = this.findEasterEgg();
            if (this.mode === 'daily') this.markDailyDone();
            this.refreshProgress(true);
            if (this.showFireworks) setTimeout(() => this.launchFireworks(), 100);
          };
          const minDelay = new Promise(r => setTimeout(r, 1500));
          this.gameService.endSession(this.sessionId).subscribe({
            next: (response) => {
              this.playerRank   = response.rank;
              this.totalPlayers = response.totalPlayers;
              minDelay.then(show);
            },
            error: () => minDelay.then(show)   // card still shows; rank badge hides when rank is unknown
          });
          setTimeout(show, 5000);
        } else {
          // Auto countdown to next question
          this.startCountdown(3, () => this.loadRound());
        }
      },
      error: (err) => {
        this.state        = 'error';
        this.errorMessage = "Your answer didn't reach the server, so that song won't count. " + this.describe(err);
      }
    });
  }

  getChoiceClass(choice: string): string {
    if (this.state !== 'answered' || !this.result) return '';
    const right = this.round?.kind === 'era' ? this.result.correctEra : this.result.correctTitle;
    if (choice === right) return 'correct';
    if (choice === this.selectedChoice)      return 'incorrect';
    return '';
  }

  formatTime(ms: number): string {
    return `${(ms / 1000).toFixed(1)}s`;
  }

  // Same curve as GameController.CalculatePoints: 10,000 * e^(-0.05 * seconds), floor 100
  get pointsAtStake(): number {
    if (this.state === 'answered' && this.result) return this.result.pointsEarned;
    return Math.max(100, Math.round(10000 * Math.exp(-0.05 * this.elapsedMs / 1000)));
  }

  private ensureAudioContext(): void {
    if (!this.audioContext) this.audioContext = new AudioContext();
    this.audioContext.resume().catch(() => {});
  }

  slowStart = false;
  private slowTimer: ReturnType<typeof setTimeout> | null = null;

  startGame(mode: GameMode = 'classic'): void {
    if (this.gameStarted) return;   // double-click guard
    if (mode === 'daily' && this.dailyDone) return;
    if (mode === 'album' && !this.pickedEras.size) { this.showAlbumPicker = true; return; }
    this.mode     = mode;
    this.songSet  = null;
    this.setIndex = 0;
    this.easterEgg = '';
    this.storyState = 'idle';
    this.progressBefore = this.progressAfter ?? this.progressBefore;
    this.startError = '';
    this.slowStart  = false;
    if (this.slowTimer) clearTimeout(this.slowTimer);
    this.slowTimer = setTimeout(() => { if (this.state !== 'playing') this.slowStart = true; }, 5000);
    this.ensureAudioContext();
    this.usedSongIds = [];
    this.gameStarted = true;
    this.loadPreviousBest();

    const set$: Observable<SongSet | null> =
      mode === 'daily'     ? this.gameService.getSet('daily') :
      mode === 'challenge' ? of(this.challenge) : of(null);

    set$.subscribe({ next: set => {
    this.songSet   = set?.songIds?.length ? set.songIds : null;
    if (set?.date) this.dailyDate = set.date;
    this.gameService.startSession(this.playerId, mode, mode === 'challenge' ? this.challenge?.from : undefined).subscribe({
      next: (response) => {
        this.sessionId = response.sessionId;
        this.buildImageQueue();
        this.state = 'countdown';

        this.fetchRound().subscribe({
          next: (round) => {
            this.slowStart = false;
            this.startCountdown(3, () => this.startPlaying(round));
          },
          error: (err) => this.failStart(err)
        });
      },
      error: (err) => this.failStart(err)
    });
    }, error: (err) => this.failStart(err) });
  }

  startError = '';

  private failStart(err?: unknown): void {
    this.gameStarted = false;
    this.state       = 'loading';
    this.slowStart   = false;
    this.startError  = this.describe(err);
  }

  private describe(err: unknown): string {
    const status = err instanceof HttpErrorResponse ? err.status : -1;
    if (status === 503) return 'No songs are available right now. Ask whoever runs Swiftology to switch some albums back on.';
    if (status === 0 && !navigator.onLine) return "You're offline. Reconnect and try again.";
    if (status === 0) return "Couldn't reach the game server. It may be waking up, so try again in a moment.";
    return 'Something went wrong on the server. Try again in a moment.';
  }

  restartGame(): void {
    if (!this.gameOver) return;     // double-tap guard: the first tap clears gameOver
    const mode = this.mode === 'challenge' || this.mode === 'daily' ? 'classic' : this.mode;
    this.challenge = this.mode === 'challenge' ? null : this.challenge;
    this.goHome();
    this.startGame(mode);
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
    this.roundResults      = [];
    this.streak            = 0;
    this.shareState        = 'idle';
    this.showAllMissed     = false;
    this.state             = 'loading';
    this.paused            = false;
    this.slowStart         = false;
    this.stopCountdown();
    this.stopAudio();
    this.stopTimer();
  }

  get displayQuestion(): number {
    return Math.min(this.currentQuestion, this.questionsPerGame);
  }

  get isNewBest(): boolean {
    return this.auth.isAuthenticated() && this.previousBest !== null && this.totalScore > this.previousBest;
  }

  // Strongest true fact first, so the headline never contradicts the stats below it
  get rankMessage(): string {
    if (this.playerRank <= 0)   return '';
    if (this.isNewBest)         return 'New personal best!';
    // On a small board almost every game is "top 10": only praise rank when it beat most games
    if (this.beatPercent < 50 && this.playerRank > 1) {
      return `${this.correctCount} of ${this.questionsAnswered} right. See what you missed below.`;
    }
    if (this.playerRank === 1)  return '👑 NEW HIGH SCORE!';
    if (this.playerRank <= 3)   return '🏆 TOP 3 ALL TIME!';
    if (this.playerRank <= 10)  return '⭐ TOP 10 ALL TIME!';
    if (this.playerRank <= 50)  return '🎵 TOP 50 ALL TIME!';
    return '🎤 GREAT EFFORT!';
  }

  get showFireworks(): boolean {
    return this.playerRank > 0 && (this.playerRank <= 3 || this.isNewBest);
  }

  get accuracyPercent(): number {
    if (this.questionsAnswered === 0) return 0;
    return Math.round((this.correctCount / this.questionsAnswered) * 100);
  }

  // ── Audio ──────────────────────────────────────────────────────────────────

  private startAudio(url: string, startAt: number): void {
    this.audio             = new Audio(url);
    this.audio.crossOrigin = 'anonymous';
    this.audio.currentTime = startAt;

    // Set up AudioContext + AnalyserNode
    if (!this.audioContext) this.audioContext = new AudioContext();
    this.analyser     = this.audioContext.createAnalyser();
    this.analyser.fftSize               = 512;
    this.analyser.smoothingTimeConstant = 0.7;
    this.analyser.minDecibels           = -85;
    this.analyser.maxDecibels           = -10;

    this.audioSource = this.audioContext.createMediaElementSource(this.audio);
    this.audioSource.connect(this.analyser);
    this.analyser.connect(this.audioContext.destination);

    // 👈 Start timer only when audio actually begins playing
    this.audio.addEventListener('playing', () => {
      this.clearPlayingTimeout();
      this.needsTap = false;
      if (this.state === 'playing' && !this.timerInterval && !this.paused && !this.panelHidden && !document.hidden) {
        this.startTimer();
      }
    }, { once: true });

    // If the clip hasn't started within 2.5s (autoplay blocked, slow network), ask for a tap
    // rather than leaving a frozen timer. Scoring only starts once audio is actually playing.
    this.playingTimeout = setTimeout(() => { if (this.state === 'playing') this.needsTap = true; }, 2500);
    this.audio.play().catch(() => { if (this.state === 'playing') this.needsTap = true; });
    this.startVisualiser();
    this.announcement = `Song ${this.currentQuestion} of ${this.questionsPerGame}. Listen, then press 1 to 4.`;
  }

  private stopAudio(): void {
    this.stopVisualiser();

    if (this.audio) {
      this.audio.pause();
      this.audio.src = '';
      this.audio     = null;
    }

    if (this.audioSource) {
      this.audioSource.disconnect();
      this.audioSource = null;
    }

    // keep the AudioContext: it was unlocked by a tap and iOS won't unlock a new one from a timer
    this.clearPlayingTimeout();
    this.needsTap   = false;
    this.clipFailed = false;
    this.analyser   = null;
  }

  private startVisualiser(): void {
    const dataArray = new Uint8Array(this.analyser!.frequencyBinCount); // 32 values

    const tick = () => {
      this.analyser!.getByteFrequencyData(dataArray);

      this.barHeights = Array.from({ length: 28 }, (_, i) => {
        // Only use bottom 55% of bins — above that is mostly silence in music
        const bucket  = Math.floor((i / 28) * dataArray.length * 0.55);
        const raw     = dataArray[bucket] / 255;
        const boosted = Math.pow(raw, 0.45);  // more aggressive lift on loud signals but quiet passages still drop low

        return 2 + boosted * 50;   // lower floor, higher ceiling
      });

      this.animFrameId = requestAnimationFrame(tick);
    };

    tick();
  }

  private stopVisualiser(): void {
    if (this.animFrameId) {
      cancelAnimationFrame(this.animFrameId);
      this.animFrameId = 0;
    }
    this.barHeights = new Array(28).fill(0);
  }

  // ── Timer ──────────────────────────────────────────────────────────────────

  private startTimer(alreadyElapsed = 0): void {
    this.startTime     = Date.now() - alreadyElapsed;
    this.timerInterval = setInterval(() => {
      this.elapsedMs = Date.now() - this.startTime;

      // Auto-timeout after 10 seconds
      if (this.elapsedMs >= 10000 && this.state === 'playing') {
        this.stopTimer();
        this.selectChoice('__timeout__');
      }
    }, 50);
  }

  private stopTimer(): void {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
      this.timerInterval = null;
    }
  }

  // ── Countdown ──────────────────────────────────────────────────────────────

  private countdownDone: (() => void) | null = null;

  private startCountdown(seconds: number, onComplete: () => void): void {
    this.countdownDone  = onComplete;
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
    if (!canvas || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

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

      if (frame % 40 === 0 && frame < 240) {
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
      if (frame > 240 && particles.length === 0) { this.fireworksAnimationId = 0; return; }
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

  get remainingMs(): number {
    return Math.max(0, 10000 - this.elapsedMs);
  }

  // ── Eras, round dots, results ──────────────────────────────────────────────

  private loadPreviousBest(): void {
    this.previousBest = null;
    if (!this.auth.isAuthenticated()) return;
    this.gameService.getMyBest(this.playerId).subscribe({
      next:  (best) => { this.previousBest = best?.entry.pointsEarned ?? null; },
      error: ()     => {}
    });
  }

  era(album?: string | null): Era { return eraFor(album); }
  artworkLarge = artworkLarge;

  get currentEra(): Era | null {
    return this.state === 'answered' && this.result ? eraFor(this.result.album) : null;
  }

  get resultStatus(): string {
    if (!this.result) return '';
    if (this.selectedChoice === '__timeout__') return "Time's up";
    return this.result.isCorrect ? 'Correct!' : 'Not quite';
  }

  get roundDots(): Array<'correct' | 'wrong' | 'pending'> {
    return Array.from({ length: this.questionsPerGame }, (_, i) => {
      const r = this.roundResults[i];
      return r ? (r.isCorrect ? 'correct' : 'wrong') : 'pending';
    });
  }

  roundDotColor(i: number): string | null {
    const r = this.roundResults[i];
    return r?.isCorrect ? eraFor(r.album).color : null;
  }

  get roundDotsLabel(): string {
    return `${this.correctCount} correct of ${this.questionsAnswered} answered`;
  }

  // Wordle-style: era square for a right answer, black for a miss
  get shareGrid(): string {
    return this.roundResults.map(r => r.isCorrect ? eraFor(r.album).square : '⬛').join('');
  }

  showAllMissed = false;

  get missedShown(): SubmitAnswerResponse[] {
    return this.showAllMissed ? this.missed : this.missed.slice(0, 3);
  }

  get missed(): SubmitAnswerResponse[] {
    return this.roundResults.filter(r => !r.isCorrect);
  }

  get bestLine(): string | null {
    if (!this.auth.isAuthenticated()) return null;
    if (this.previousBest === null) return 'Your first game on the board';
    const delta = this.totalScore - this.previousBest;
    return delta > 0
      ? `New personal best, up ${delta.toLocaleString()}`
      : `Your best: ${this.previousBest.toLocaleString()}`;
  }

  get beatPercent(): number {
    if (this.totalPlayers < 2 || this.playerRank < 1) return 0;
    return Math.round(((this.totalPlayers - this.playerRank) / this.totalPlayers) * 100);
  }

  async share(): Promise<void> {
    const text = `${this.shareLabel} ${this.correctCount}/${this.questionsAnswered} · ${this.totalScore.toLocaleString()} pts\n${this.shareGrid}\n${this.ranked ? 'Can you beat me? ' : ''}${this.shareLink}`;
    try {
      if (navigator.share) {
        await navigator.share({ text });
        this.shareState = 'shared';
      } else {
        await navigator.clipboard.writeText(text);
        this.shareState = 'copied';
      }
    } catch {
      return; // user cancelled the share sheet
    }
    setTimeout(() => this.shareState = 'idle', 2500);
  }

  // ── Modes, challenges, progress ─────────────────────────────────────────────

  get ranked(): boolean { return this.mode === 'classic' || this.mode === 'daily' || this.mode === 'challenge'; }

  // Ranked games share a challenge: the friend plays the same songs
  get shareLink(): string {
    return this.ranked && this.sessionId
      ? `https://swiftology.uk/?challenge=${this.sessionId}&from=share`
      : 'https://swiftology.uk/?from=share';
  }

  get shareLabel(): string {
    if (this.mode === 'daily') return `Daily Swiftle ${this.dailyLabel}`;
    if (this.mode === 'eras') return 'Swiftology · Which album?';
    if (this.mode === 'album') return 'Swiftology · Album mode';
    return 'Swiftology';
  }

  get dailyLabel(): string {
    const d = this.dailyDate ? new Date(this.dailyDate + 'T00:00:00') : new Date();
    return d.toLocaleDateString(undefined, { day: 'numeric', month: 'short' });
  }

  get modeLabel(): string {
    return { classic: '', daily: `Daily Swiftle · ${this.dailyLabel}`, challenge: `Challenge from ${this.challenge?.challenger ?? 'a friend'}`,
             album: 'Album mode', eras: 'Which album?' }[this.mode];
  }

  // ── Daily: one go per day ──

  private get todayKey(): string { return new Date().toLocaleDateString('en-CA'); }

  get dailyDone(): boolean {
    if (this.auth.isAuthenticated()) return !!this.progressBefore?.playedDailyToday;
    try { return localStorage.getItem('swiftgame_daily') === this.todayKey; } catch { return false; }
  }

  private markDailyDone(): void {
    try { localStorage.setItem('swiftgame_daily', this.todayKey); } catch { /* private mode */ }
  }

  get dailyStreak(): number { return (this.progressAfter ?? this.progressBefore)?.streak ?? 0; }

  // ── Album mode: pick eras, play every album in them ──

  openAlbumPicker(): void {
    this.showAlbumPicker = !this.showAlbumPicker;
    if (!this.albums.length) this.gameService.getAlbums().subscribe({ next: a => this.albums = a, error: () => {} });
  }

  toggleEra(key: string): void {
    this.pickedEras.has(key) ? this.pickedEras.delete(key) : this.pickedEras.add(key);
  }

  get pickableEras() {
    const present = new Set(this.albums.map(a => a.era));
    return this.eraList.filter(e => present.has(e.key));
  }

  get selectedAlbumIds(): string[] {
    return this.albums.filter(a => a.era && this.pickedEras.has(a.era)).map(a => a.id);
  }

  // ── Challenge result ──

  get challengeVerdict(): string {
    if (this.mode !== 'challenge' || !this.challenge) return '';
    const them = this.challenge.challengerScore ?? 0;
    const name = this.challenge.challenger;
    const who  = !name || name === 'A friend' ? 'your friend' : name;
    if (this.totalScore > them) return `You beat ${who} by ${(this.totalScore - them).toLocaleString()}!`;
    if (this.totalScore === them) return `A dead heat with ${who}!`;
    return `${who} wins this one by ${(them - this.totalScore).toLocaleString()}. Rematch?`;
  }

  get challengerGrid(): string {
    return (this.challenge?.challengerRounds ?? []).map(r => r.isCorrect ? '🟩' : '⬛').join('');
  }

  // ── Bracelet beads earned this game ──

  private refreshProgress(after = false): void {
    if (!this.auth.isAuthenticated()) { this.progressBefore = this.progressAfter = null; return; }
    this.gameService.getProgress(this.playerId).subscribe({
      next: p => after ? this.progressAfter = p : (this.progressBefore = p),
      error: () => {}
    });
  }

  get newBeads(): string[] {
    if (!this.progressBefore || !this.progressAfter) return [];
    const had = new Set(this.progressBefore.beads.filter(b => b.earned).map(b => b.key));
    return this.progressAfter.beads.filter(b => b.earned && !had.has(b.key)).map(b => b.label);
  }

  get newGoldBead(): boolean {
    return !!this.progressBefore && !!this.progressAfter && this.progressAfter.goldBeads > this.progressBefore.goldBeads;
  }

  // ── Easter eggs ──

  private findEasterEgg(): string {
    if (this.questionsAnswered >= 10 && this.correctCount === this.questionsAnswered) return 'Ten out of ten. Mastermind behaviour. 🧠';
    if (String(this.totalScore).includes('13')) return 'There\'s a 13 in your score. Taylor would approve. 🫶';
    if (new Date().getMinutes() === 13) return 'You finished at 13 past. That\'s a sign. ✨';
    return '';
  }

  // ── Story image ──

  async saveStory(): Promise<void> {
    if (this.storyState === 'working') return;
    this.storyState = 'working';
    const accent = getComputedStyle(document.documentElement).getPropertyValue('--skin-1').trim() || '#ff69b4';
    const input = { score: this.totalScore, correct: this.correctCount, total: this.questionsAnswered,
                    rounds: this.roundResults, label: this.modeLabel || 'Guess the song', accent };
    const blob = (await drawStory(input)) ?? (await drawStory({ ...input, covers: false }));
    if (!blob) { this.storyState = 'failed'; return; }

    const file = new File([blob], 'swiftology-story.png', { type: 'image/png' });
    try {
      if (navigator.canShare?.({ files: [file] })) {
        await navigator.share({ files: [file], text: this.shareLink });
      } else {
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = file.name;
        a.click();
        setTimeout(() => URL.revokeObjectURL(a.href), 5000);
      }
      this.storyState = 'done';
    } catch {
      this.storyState = 'idle';   // share sheet cancelled
    }
  }
}
