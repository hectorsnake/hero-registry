import { DOCUMENT, inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';

/** A page with a form that can tell whether leaving would lose the user's edits. */
export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}

export const UNSAVED_CHANGES_MESSAGE = 'You have unsaved changes. Leave this page and lose them?';

/** Asks before leaving a form with unsaved changes (the browser's own confirm: accessible and blocking). */
export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component) =>
  !component.hasUnsavedChanges() ||
  (inject(DOCUMENT).defaultView?.confirm(UNSAVED_CHANGES_MESSAGE) ?? true);
