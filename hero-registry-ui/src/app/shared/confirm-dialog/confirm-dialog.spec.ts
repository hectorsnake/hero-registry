import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ConfirmDialog } from './confirm-dialog';

@Component({
  imports: [ConfirmDialog],
  template: `
    <app-confirm-dialog
      [(open)]="open"
      title="Delete Superman?"
      message="This hero will no longer appear in the registry."
      confirmLabel="Delete"
      [pending]="pending()"
      (confirmed)="confirmed = confirmed + 1"
    />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class Host {
  readonly open = signal(false);
  readonly pending = signal(false);
  confirmed = 0;
}

describe('ConfirmDialog', () => {
  async function render() {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const dialog = () => element.querySelector('dialog')!;
    const button = (name: string) =>
      Array.from(element.querySelectorAll('dialog button')).find(
        (b) => b.textContent?.trim() === name,
      ) as HTMLButtonElement | undefined;
    const open = async () => {
      fixture.componentInstance.open.set(true);
      await fixture.whenStable();
    };
    return { fixture, host: fixture.componentInstance, element, dialog, button, open };
  }

  it('is closed until opened', async () => {
    const { dialog } = await render();

    expect(dialog().open).toBe(false);
  });

  it('opens with the title and message, labelled for screen readers', async () => {
    const view = await render();

    await view.open();

    expect(view.dialog().open).toBe(true);
    expect(view.dialog().textContent).toContain('Delete Superman?');
    expect(view.dialog().textContent).toContain('This hero will no longer appear in the registry.');
    const labelledBy = view.dialog().getAttribute('aria-labelledby')!;
    expect(view.element.querySelector(`#${labelledBy}`)?.textContent?.trim()).toBe(
      'Delete Superman?',
    );
  });

  it('focuses Cancel by default, the safe choice', async () => {
    const view = await render();

    await view.open();

    expect(document.activeElement).toBe(view.button('Cancel'));
  });

  it('Cancel closes without confirming', async () => {
    const view = await render();
    await view.open();

    view.button('Cancel')!.click();
    await view.fixture.whenStable();

    expect(view.host.open()).toBe(false);
    expect(view.dialog().open).toBe(false);
    expect(view.host.confirmed).toBe(0);
  });

  it('Escape (the dialog cancel event) also closes', async () => {
    const view = await render();
    await view.open();

    view.dialog().dispatchEvent(new Event('cancel', { cancelable: true }));
    await view.fixture.whenStable();

    expect(view.host.open()).toBe(false);
  });

  it('the confirm button emits confirmed; the owner closes it when the work is done', async () => {
    const view = await render();
    await view.open();

    view.button('Delete')!.click();

    expect(view.host.confirmed).toBe(1);
    expect(view.host.open()).toBe(true);
  });

  it('disables the confirm button while the action is pending', async () => {
    const view = await render();
    await view.open();

    view.host.pending.set(true);
    await view.fixture.whenStable();

    expect(view.button('Delete')!.disabled).toBe(true);
  });
});
