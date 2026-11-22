import { Injectable, signal } from '@angular/core';

export type ToastKind = 'info' | 'success' | 'warning' | 'error';

export interface Toast {
  id: number;
  kind: ToastKind;
  text: string;
}

/** How long a toast stays on screen. */
export const TOAST_DURATION_MS = 5_000;

/** Short notifications for the whole app, rendered by `<app-toast-outlet>`. */
@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly #toasts = signal<readonly Toast[]>([]);
  #nextId = 1;

  readonly toasts = this.#toasts.asReadonly();

  /** Shows a toast and returns its id. A message already on screen is not shown twice. */
  show(text: string, kind: ToastKind = 'info'): number {
    const existing = this.#toasts().find((t) => t.text === text && t.kind === kind);
    if (existing) {
      return existing.id;
    }

    const toast: Toast = { id: this.#nextId++, kind, text };
    this.#toasts.update((toasts) => [...toasts, toast]);
    setTimeout(() => this.dismiss(toast.id), TOAST_DURATION_MS);
    return toast.id;
  }

  dismiss(id: number): void {
    this.#toasts.update((toasts) => toasts.filter((t) => t.id !== id));
  }
}
