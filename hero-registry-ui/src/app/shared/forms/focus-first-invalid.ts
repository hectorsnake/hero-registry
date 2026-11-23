import { FieldTree } from '@angular/forms/signals';

/**
 * Moves focus to the first invalid field, in the order given (the visual order of the form). Used after
 * a failed submit, so keyboard and screen reader users land on what needs fixing.
 */
export function focusFirstInvalid(fields: readonly FieldTree<unknown>[]): void {
  fields
    .find((field) => field().invalid())?.()
    .focusBoundControl();
}
