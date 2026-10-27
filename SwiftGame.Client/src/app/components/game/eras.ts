// Album name -> era accent. Colours are chosen light enough to read on the dark stage (>= 4.5:1).
export interface Era { key: string; label: string; color: string; }

const ERAS: Array<Era & { match: RegExp }> = [
  { key: 'ttpd',      label: 'The Tortured Poets Department', color: '#e6dfd0', match: /tortured poets/i },
  { key: 'showgirl',  label: 'The Life of a Showgirl',        color: '#ffb067', match: /showgirl/i },
  { key: 'midnights', label: 'Midnights',                     color: '#8ea1e8', match: /midnights/i },
  { key: 'evermore',  label: 'evermore',                      color: '#d9966a', match: /evermore/i },
  { key: 'folklore',  label: 'folklore',                      color: '#c9c9c9', match: /folklore/i },
  { key: 'lover',     label: 'Lover',                         color: '#f7b2d4', match: /lover/i },
  { key: 'rep',       label: 'reputation',                    color: '#b8c0c8', match: /reputation/i },
  { key: '1989',      label: '1989',                          color: '#8ec5f5', match: /1989/ },
  { key: 'red',       label: 'Red',                           color: '#ff6b6b', match: /^red\b/i },
  { key: 'speaknow',  label: 'Speak Now',                     color: '#c084e8', match: /speak now/i },
  { key: 'fearless',  label: 'Fearless',                      color: '#f0cf5a', match: /fearless/i },
  { key: 'debut',     label: 'Taylor Swift',                  color: '#8fd9b6', match: /^taylor swift/i },
];

const FALLBACK: Era = { key: 'swift', label: '', color: '#ff69b4' };

export function eraFor(album: string | undefined | null): Era {
  if (!album) return FALLBACK;
  return ERAS.find(e => e.match.test(album)) ?? { ...FALLBACK, label: album };
}

// iTunes artwork URLs embed their size; ask for a larger render of the same image.
export function artworkLarge(url: string): string {
  return url.replace(/\d+x\d+bb/, '600x600bb');
}
