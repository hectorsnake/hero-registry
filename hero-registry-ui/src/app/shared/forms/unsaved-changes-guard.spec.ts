import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import {
  HasUnsavedChanges,
  UNSAVED_CHANGES_MESSAGE,
  unsavedChangesGuard,
} from './unsaved-changes-guard';

describe('unsavedChangesGuard', () => {
  afterEach(() => vi.restoreAllMocks());

  function leave(component: HasUnsavedChanges): boolean {
    return TestBed.runInInjectionContext(() =>
      unsavedChangesGuard(
        component,
        {} as ActivatedRouteSnapshot,
        {} as RouterStateSnapshot,
        {} as RouterStateSnapshot,
      ),
    ) as boolean;
  }

  it('lets the user leave without asking when nothing changed', () => {
    const confirm = vi.spyOn(window, 'confirm');

    expect(leave({ hasUnsavedChanges: () => false })).toBe(true);
    expect(confirm).not.toHaveBeenCalled();
  });

  it('asks when there are unsaved changes, and stays if the user says no', () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);

    expect(leave({ hasUnsavedChanges: () => true })).toBe(false);
    expect(confirm).toHaveBeenCalledWith(UNSAVED_CHANGES_MESSAGE);
  });

  it('leaves when the user confirms', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    expect(leave({ hasUnsavedChanges: () => true })).toBe(true);
  });
});
