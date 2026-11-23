import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  input,
  model,
  output,
  viewChild,
} from '@angular/core';

let nextId = 0;

/**
 * A modal confirmation on the native `<dialog>` (focus trap, Escape and backdrop for free). Cancel is
 * focused first. Emits `confirmed`; the owner does the work, then closes it with `open.set(false)`.
 */
@Component({
  selector: 'app-confirm-dialog',
  template: `
    <dialog
      #dialog
      class="confirm"
      [attr.aria-labelledby]="titleId"
      [attr.aria-describedby]="messageId"
      (cancel)="dismiss($event)"
      (close)="open.set(false)"
    >
      <h2 class="confirm__title" [id]="titleId">{{ title() }}</h2>
      <p class="confirm__message" [id]="messageId">{{ message() }}</p>
      <div class="confirm__actions">
        <button #cancelButton type="button" class="confirm__cancel" (click)="open.set(false)">
          Cancel
        </button>
        <button
          type="button"
          class="confirm__confirm"
          [attr.data-danger]="danger()"
          [disabled]="pending()"
          [attr.aria-busy]="pending()"
          (click)="confirmed.emit()"
        >
          {{ confirmLabel() }}
        </button>
      </div>
    </dialog>
  `,
  styleUrl: './confirm-dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmDialog {
  readonly open = model(false);
  readonly title = input.required<string>();
  readonly message = input<string>('');
  readonly confirmLabel = input('Confirm');
  readonly danger = input(true);
  readonly pending = input(false);
  readonly confirmed = output();

  protected readonly titleId = `confirm-title-${++nextId}`;
  protected readonly messageId = `confirm-message-${nextId}`;
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly cancelButton = viewChild.required<ElementRef<HTMLButtonElement>>('cancelButton');

  constructor() {
    // Syncing a signal to the DOM element is what effects are for.
    effect(() => {
      const dialog = this.dialog().nativeElement;
      if (this.open() && !dialog.open) {
        showModal(dialog);
        this.cancelButton().nativeElement.focus();
      } else if (!this.open() && dialog.open) {
        closeDialog(dialog);
      }
    });
  }

  protected dismiss(event: Event): void {
    event.preventDefault(); // close through the model, so `open` stays the single source of truth
    this.open.set(false);
  }
}

/** `showModal()`, or the `open` attribute where it is not implemented (jsdom). */
function showModal(dialog: HTMLDialogElement): void {
  if (typeof dialog.showModal === 'function') {
    dialog.showModal();
  } else {
    dialog.setAttribute('open', '');
  }
}

/** `close()`, or removing the `open` attribute where it is not implemented. */
function closeDialog(dialog: HTMLDialogElement): void {
  if (typeof dialog.close === 'function') {
    dialog.close();
  } else {
    dialog.removeAttribute('open');
  }
}
