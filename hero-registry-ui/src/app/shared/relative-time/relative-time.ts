import { Pipe, PipeTransform } from '@angular/core';

const FORMAT = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });

const UNITS: readonly [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 365 * 24 * 3600],
  ['month', 30 * 24 * 3600],
  ['day', 24 * 3600],
  ['hour', 3600],
  ['minute', 60],
];

/** "3 days ago", "yesterday", "just now" (under a minute). Empty for a missing or invalid date. */
export function relativeTime(iso: string | null | undefined, now: Date): string {
  const time = iso ? new Date(iso).getTime() : Number.NaN;
  if (Number.isNaN(time)) {
    return '';
  }

  const seconds = Math.round((time - now.getTime()) / 1000);
  for (const [unit, size] of UNITS) {
    if (Math.abs(seconds) >= size) {
      return FORMAT.format(Math.round(seconds / size), unit);
    }
  }

  return 'just now';
}

/** `{{ hero.createdAt | relativeTime }}`. Pure: it formats against the time of rendering. */
@Pipe({ name: 'relativeTime' })
export class RelativeTimePipe implements PipeTransform {
  transform(iso: string | null | undefined): string {
    return relativeTime(iso, new Date());
  }
}
