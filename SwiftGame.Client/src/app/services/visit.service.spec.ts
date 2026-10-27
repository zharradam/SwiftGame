import { describe, it, expect } from 'vitest';
import { sourceOf } from './visit.service';

describe('sourceOf', () => {
  it('labels our own share link', () => expect(sourceOf('?from=share', '')).toBe('Shared link'));
  it('treats no referrer as Direct', () => expect(sourceOf('', '')).toBe('Direct'));
  it('groups search engines', () => expect(sourceOf('', 'https://www.google.com.au/')).toBe('Google'));
  it('groups t.co as X', () => expect(sourceOf('', 'https://t.co/abc')).toBe('X'));
  it('treats our own site as Direct', () => expect(sourceOf('', 'https://swiftology.uk/')).toBe('Direct'));
  it('falls back to the host name', () => expect(sourceOf('', 'https://example.org/x')).toBe('example.org'));
});
