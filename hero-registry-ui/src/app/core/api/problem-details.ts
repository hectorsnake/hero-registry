import { HttpErrorResponse } from '@angular/common/http';

/** An RFC 9457 problem, as every API error is returned (T11). */
export interface ProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  /** Stable machine-readable code, for example `user.duplicate` or `auth.refresh_race`. */
  code?: string;
  traceId?: string;
}

/** A 400 or 409 problem: messages keyed by camelCase field name (paths such as `superpowers[0]`). */
export interface ValidationProblemDetails extends ProblemDetails {
  errors: Record<string, string[]>;
}

/** Messages for the fields of one form, ready to show next to each field. */
export type FieldErrors<F extends string> = Partial<Record<F, string[]>>;

export function isProblemDetails(value: unknown): value is ProblemDetails {
  return (
    typeof value === 'object' &&
    value !== null &&
    typeof (value as ProblemDetails).title === 'string' &&
    typeof (value as ProblemDetails).status === 'number'
  );
}

/** The problem in an HTTP error (or the value itself), or `null` when it is not one. */
export function toProblem(error: unknown): ProblemDetails | null {
  const body = error instanceof HttpErrorResponse ? error.error : error;
  return isProblemDetails(body) ? body : null;
}

/** The problem's code (`auth.refresh_race`, ...), or `null`. */
export function problemCode(error: unknown): string | null {
  return toProblem(error)?.code ?? null;
}

/**
 * The title of the problem in `error`, or `fallback`. Raw server text (an HTML error page from a proxy,
 * a stack trace) is never shown to the user.
 */
export function problemTitle(error: unknown, fallback: string): string {
  return toProblem(error)?.title ?? fallback;
}

/**
 * Maps a validation problem's `errors` onto the given form fields. `superpowers[0]` belongs to
 * `superpowers`; keys that are not fields of the form (such as `body`) are ignored.
 */
export function toFieldErrors<F extends string>(
  error: unknown,
  fields: readonly F[],
): FieldErrors<F> {
  const errors = (toProblem(error) as Partial<ValidationProblemDetails> | null)?.errors;
  if (typeof errors !== 'object' || errors === null) {
    return {};
  }

  const result: FieldErrors<F> = {};
  for (const [key, messages] of Object.entries(errors)) {
    const field = fields.find((f) => f === fieldOf(key));
    if (field && Array.isArray(messages)) {
      result[field] = [...(result[field] ?? []), ...messages];
    }
  }

  return result;
}

/** `superpowers[0]` → `superpowers`, `address.city` → `address`. */
function fieldOf(key: string): string {
  return key.split(/[[.]/, 1)[0];
}
