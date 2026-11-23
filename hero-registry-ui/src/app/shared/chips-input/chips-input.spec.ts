import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormField, form, validate } from '@angular/forms/signals';
import { ChipsInput } from './chips-input';

@Component({
  imports: [ChipsInput],
  template: `<app-chips-input inputId="powers" [(value)]="powers" [limit]="limit()" />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class Host {
  readonly powers = signal<string[]>(['Flight']);
  readonly limit = signal(10);
}

@Component({
  imports: [ChipsInput, FormField],
  template: `<app-chips-input inputId="powers" [formField]="heroForm.superpowers" />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class FormHost {
  readonly model = signal({ superpowers: [] as string[] });
  // required() treats [] as a value, so an empty list is invalid through validate().
  readonly heroForm = form(this.model, (s) =>
    validate(s.superpowers, ({ value }) => (value().length ? undefined : { kind: 'empty' })),
  );
}

describe('ChipsInput', () => {
  async function render() {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const input = () => element.querySelector<HTMLInputElement>('#powers')!;
    const chips = () =>
      Array.from(element.querySelectorAll('[data-chip]')).map((c) => c.getAttribute('data-chip'));
    const key = async (key: string) => {
      input().dispatchEvent(new KeyboardEvent('keydown', { key, cancelable: true }));
      await fixture.whenStable();
    };
    const type = async (text: string) => {
      input().value = text;
      input().dispatchEvent(new Event('input'));
      await fixture.whenStable();
    };
    return { fixture, host: fixture.componentInstance, element, input, chips, key, type };
  }

  it('renders one chip per value, each with a named remove button', async () => {
    const { element, chips } = await render();

    expect(chips()).toEqual(['Flight']);
    expect(element.querySelector('button[aria-label="Remove Flight"]')).not.toBeNull();
  });

  it('Enter adds the trimmed text as a chip and clears the input', async () => {
    const view = await render();

    await view.type('  Super strength ');
    await view.key('Enter');

    expect(view.host.powers()).toEqual(['Flight', 'Super strength']);
    expect(view.input().value).toBe('');
  });

  it('clears the text even when Enter follows the typing before any re-render (fast typing, paste)', async () => {
    const view = await render();

    view.input().value = 'Weather control';
    view.input().dispatchEvent(new Event('input'));
    view.input().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', cancelable: true }));
    await view.fixture.whenStable();

    expect(view.input().value).toBe('');
    expect(view.chips()).toEqual(['Flight', 'Weather control']);
  });

  it('a comma also adds a chip', async () => {
    const view = await render();

    await view.type('X-ray vision');
    await view.key(',');

    expect(view.host.powers()).toEqual(['Flight', 'X-ray vision']);
  });

  it('Enter does not submit the surrounding form', async () => {
    const view = await render();
    await view.type('Speed');
    const event = new KeyboardEvent('keydown', { key: 'Enter', cancelable: true });

    view.input().dispatchEvent(event);

    expect(event.defaultPrevented).toBe(true);
  });

  it('ignores blank text', async () => {
    const view = await render();

    await view.type('   ');
    await view.key('Enter');

    expect(view.host.powers()).toEqual(['Flight']);
  });

  it('blocks duplicates, ignoring case', async () => {
    const view = await render();

    await view.type('flight');
    await view.key('Enter');

    expect(view.host.powers()).toEqual(['Flight']);
    expect(view.element.textContent).toContain('already added');
  });

  it('Backspace on an empty input removes the last chip', async () => {
    const view = await render();

    await view.key('Backspace');

    expect(view.host.powers()).toEqual([]);
  });

  it('Backspace while typing only edits the text', async () => {
    const view = await render();
    await view.type('Spe');

    await view.key('Backspace');

    expect(view.host.powers()).toEqual(['Flight']);
  });

  it('the remove button removes that chip', async () => {
    const view = await render();

    view.element.querySelector<HTMLButtonElement>('button[aria-label="Remove Flight"]')!.click();
    await view.fixture.whenStable();

    expect(view.host.powers()).toEqual([]);
  });

  it('adds pending text when the input loses focus', async () => {
    const view = await render();
    await view.type('Healing');

    view.input().dispatchEvent(new Event('blur'));
    await view.fixture.whenStable();

    expect(view.host.powers()).toEqual(['Flight', 'Healing']);
  });

  it('disables the input at the limit (10 by default)', async () => {
    const view = await render();
    view.host.powers.set(Array.from({ length: 10 }, (_, i) => `Power ${i}`));
    await view.fixture.whenStable();

    expect(view.input().disabled).toBe(true);
    expect(view.element.textContent).toContain('10 of 10');
  });

  it('shows the invalid style only once the field is touched, like the other inputs', async () => {
    const fixture = TestBed.createComponent(FormHost);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const box = () => element.querySelector('.chips')!;
    expect(fixture.componentInstance.heroForm.superpowers().invalid()).toBe(true);

    expect(box().getAttribute('data-invalid')).toBe('false');

    element.querySelector('#powers')!.dispatchEvent(new Event('blur'));
    await fixture.whenStable();
    expect(box().getAttribute('data-invalid')).toBe('true');
  });

  it('works as a Signal Forms control through [formField]', async () => {
    const fixture = TestBed.createComponent(FormHost);
    await fixture.whenStable();
    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      '#powers',
    )!;

    input.value = 'Flight';
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', cancelable: true }));
    input.dispatchEvent(new Event('blur'));
    await fixture.whenStable();

    expect(fixture.componentInstance.model().superpowers).toEqual(['Flight']);
    expect(fixture.componentInstance.heroForm.superpowers().touched()).toBe(true);
  });
});
