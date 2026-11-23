import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  input,
  model,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormValueControl } from '@angular/forms/signals';

/**
 * A list of short texts edited as chips (for example superpowers). Enter or comma adds the typed text,
 * Backspace on an empty input removes the last chip, and duplicates (ignoring case) are refused.
 * It is a Signal Forms custom control: bind it with `[formField]`, or with `[(value)]`.
 */
@Component({
  selector: 'app-chips-input',
  templateUrl: './chips-input.html',
  styleUrl: './chips-input.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChipsInput implements FormValueControl<string[]> {
  readonly value = model.required<string[]>();
  /** Set by `[formField]` when the field is disabled. */
  readonly disabled = input(false);
  /** Set by `[formField]` when the field is invalid. */
  readonly invalid = input(false);
  /** Set by `[formField]` once the user has interacted with the field (or submitted the form). */
  readonly touched = input(false);
  /** The text input's id, for an external `<label for>`. */
  readonly inputId = input.required<string>();
  /** Ids of the elements that describe the input (error, hint). */
  readonly describedBy = input<string>();
  /** The most chips allowed; the input is disabled once reached. */
  readonly limit = input(10);
  /** Tells Signal Forms the user has finished interacting (marks the field as touched). */
  readonly touch = output();

  /**
   * The text box is the only source of the typed text. A `[value]` binding would not clear it when Enter
   * follows the typing before a re-render (fast typing or paste): the binding still holds its last
   * rendered '' and sees no change.
   */
  private readonly textInput = viewChild.required<ElementRef<HTMLInputElement>>('textInput');
  protected readonly notice = signal<string | null>(null);
  protected readonly full = computed(() => this.value().length >= this.limit());
  /** Like the other inputs: errors are only shown once the field has been touched. */
  protected readonly showInvalid = computed(() => this.invalid() && this.touched());

  protected onInput(): void {
    this.notice.set(null);
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' || event.key === ',') {
      event.preventDefault(); // never submit the surrounding form, never type the comma
      this.#addText();
    } else if (event.key === 'Backspace' && this.#text() === '' && this.value().length > 0) {
      this.value.update((chips) => chips.slice(0, -1));
    }
  }

  protected onBlur(): void {
    this.#addText();
    this.touch.emit();
  }

  protected remove(chip: string): void {
    this.value.update((chips) => chips.filter((c) => c !== chip));
    this.notice.set(null);
  }

  #addText(): void {
    const chip = this.#text().trim();
    if (!chip || this.full()) {
      return;
    }

    if (this.value().some((c) => c.toLowerCase() === chip.toLowerCase())) {
      this.notice.set(`"${chip}" is already added.`);
      return;
    }

    this.value.update((chips) => [...chips, chip]);
    this.textInput().nativeElement.value = '';
  }

  #text(): string {
    return this.textInput().nativeElement.value;
  }
}
