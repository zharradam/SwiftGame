import { SubmitAnswerResponse } from '../../services/game.service';
import { eraFor, artworkLarge } from './eras';

export interface StoryInput {
  score: number;
  correct: number;
  total: number;
  rounds: SubmitAnswerResponse[];
  label: string;          // e.g. "Daily Swiftle · 28 Oct" or "Swiftology"
  accent: string;         // era skin colour
  covers?: boolean;       // false: skip album art (retry path when a cover taints the canvas)
}

const W = 1080, H = 1920;

function loadImage(src: string): Promise<HTMLImageElement | null> {
  return new Promise(resolve => {
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => resolve(img);
    img.onerror = () => resolve(null);
    img.src = src;
    setTimeout(() => resolve(null), 4000);
  });
}

function roundRect(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number) {
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
}

// Story-sized (9:16) results image for Instagram / TikTok
export async function drawStory(input: StoryInput): Promise<Blob | null> {
  const canvas = document.createElement('canvas');
  canvas.width = W; canvas.height = H;
  const ctx = canvas.getContext('2d');
  if (!ctx) return null;

  // Background: night sky with an era-coloured glow
  ctx.fillStyle = '#0d0a1a';
  ctx.fillRect(0, 0, W, H);
  const glow = ctx.createRadialGradient(W / 2, 380, 40, W / 2, 380, 900);
  glow.addColorStop(0, input.accent + '66');
  glow.addColorStop(1, 'transparent');
  ctx.fillStyle = glow;
  ctx.fillRect(0, 0, W, H);

  const font = (weight: number, size: number) => `${weight} ${size}px -apple-system, "Segoe UI", sans-serif`;
  ctx.textAlign = 'center';

  ctx.fillStyle = input.accent;
  ctx.font = font(800, 92);
  ctx.fillText('Swiftology', W / 2, 260);
  ctx.fillStyle = 'rgba(255,255,255,0.75)';
  ctx.font = font(600, 40);
  ctx.fillText(input.label, W / 2, 330);

  const scoreGrad = ctx.createLinearGradient(200, 0, 880, 0);
  scoreGrad.addColorStop(0, '#ffd166');
  scoreGrad.addColorStop(1, input.accent);
  ctx.fillStyle = scoreGrad;
  ctx.font = font(900, 210);
  ctx.fillText(input.score.toLocaleString(), W / 2, 600);
  ctx.fillStyle = '#fff';
  ctx.font = font(700, 54);
  ctx.fillText(`${input.correct} of ${input.total} right`, W / 2, 690);

  // Era squares, one per song
  const sq = 72, gap = 14;
  const rowW = input.rounds.length * sq + (input.rounds.length - 1) * gap;
  let x = (W - rowW) / 2;
  for (const r of input.rounds) {
    ctx.fillStyle = r.isCorrect ? eraFor(r.album).color : '#2a2338';
    roundRect(ctx, x, 760, sq, sq, 14);
    ctx.fill();
    x += sq + gap;
  }

  // Album covers of the songs they got right (up to 6)
  const covers = input.covers === false ? [] : input.rounds.filter(r => r.isCorrect && r.albumArt).slice(0, 6);
  const imgs = await Promise.all(covers.map(c => loadImage(artworkLarge(c.albumArt))));
  const size = 290, cgap = 30;
  imgs.forEach((img, i) => {
    const col = i % 3, row = Math.floor(i / 3);
    const cx = (W - (3 * size + 2 * cgap)) / 2 + col * (size + cgap);
    const cy = 920 + row * (size + cgap);
    ctx.save();
    roundRect(ctx, cx, cy, size, size, 28);
    ctx.clip();
    if (img) ctx.drawImage(img, cx, cy, size, size);
    else { ctx.fillStyle = eraFor(covers[i].album).color; ctx.fillRect(cx, cy, size, size); }
    ctx.restore();
  });

  ctx.fillStyle = 'rgba(255,255,255,0.85)';
  ctx.font = font(700, 50);
  ctx.fillText('Can you beat me?', W / 2, 1700);
  ctx.fillStyle = input.accent;
  ctx.font = font(800, 56);
  ctx.fillText('swiftology.uk', W / 2, 1780);

  return new Promise(resolve => {
    try { canvas.toBlob(b => resolve(b), 'image/png'); }
    catch { resolve(null); }   // a cover without CORS headers taints the canvas
  });
}
