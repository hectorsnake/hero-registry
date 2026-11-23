import { TestBed } from '@angular/core/testing';
import { RelativeTimePipe, relativeTime } from './relative-time';

const NOW = new Date('2026-11-22T12:00:00Z');

describe('relativeTime', () => {
  it.each([
    ['2026-11-22T11:59:30Z', 'just now'],
    ['2026-11-22T11:55:00Z', '5 minutes ago'],
    ['2026-11-22T09:00:00Z', '3 hours ago'],
    ['2026-11-21T12:00:00Z', 'yesterday'],
    ['2026-11-19T12:00:00Z', '3 days ago'],
    ['2026-10-22T12:00:00Z', 'last month'],
    ['2024-11-22T12:00:00Z', '2 years ago'],
  ])('%s → %s', (iso, expected) => {
    expect(relativeTime(iso, NOW)).toBe(expected);
  });

  it('is empty for a missing date', () => {
    expect(relativeTime(null, NOW)).toBe('');
    expect(relativeTime(undefined, NOW)).toBe('');
  });

  it('is empty for an invalid date', () => {
    expect(relativeTime('not a date', NOW)).toBe('');
  });
});

describe('RelativeTimePipe', () => {
  it('formats against the current time', () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(NOW);

    const pipe = TestBed.runInInjectionContext(() => new RelativeTimePipe());

    expect(pipe.transform('2026-11-19T12:00:00Z')).toBe('3 days ago');
    vi.useRealTimers();
  });
});
