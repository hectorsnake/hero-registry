import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { FieldTree } from '@angular/forms/signals';

const FALLBACK_MESSAGE = 'This value is not valid.';

/**
 * The first error of a Signal Forms field, shown once the user has touched it (or submitted the form).
 * Put an `id` on it and point the input's `aria-describedby` at that id.
 */
@Component({
  selector: 'app-form-field-error',
  template: `
    @if (message(); as text) {
      <span class="field-error" role="alert">{{ text }}</span>
    }
  `,
  styles: `
    .field-error {
      display: block;
      margin-block-start: var(--space-1);
      color: var(--color-danger);
      font-size: 0.875rem;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormFieldError {
  readonly field = input.required<FieldTree<unknown>>();

  protected readonly message = computed(() => {
    const state = this.field()();
    const [first] = state.errors();
    return state.touched() && first ? (first.message ?? FALLBACK_MESSAGE) : null;
  });
}
