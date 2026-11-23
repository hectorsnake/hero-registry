import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { form, minLength, required } from '@angular/forms/signals';
import { FormFieldError } from './form-field-error';

@Component({
  imports: [FormFieldError],
  template: `<app-form-field-error id="name-error" [field]="nameForm.name" />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class Host {
  readonly model = signal({ name: '' });
  readonly nameForm = form(this.model, (s) => {
    required(s.name, { message: 'Name is required.' });
    minLength(s.name, 3, { message: 'Use at least 3 characters.' });
  });
}

describe('FormFieldError', () => {
  async function render() {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const host = fixture.componentInstance;
    const element = fixture.nativeElement as HTMLElement;
    const text = () => element.querySelector('app-form-field-error')?.textContent?.trim() ?? '';
    return { fixture, host, element, text };
  }

  it('shows nothing while the field is untouched, even when it is invalid', async () => {
    const { host, text } = await render();

    expect(host.nameForm.name().invalid()).toBe(true);
    expect(text()).toBe('');
  });

  it('shows the message of the first error once the field is touched', async () => {
    const { fixture, host, text } = await render();

    host.nameForm.name().markAsTouched();
    await fixture.whenStable();

    expect(text()).toBe('Name is required.');
  });

  it('follows the value: the next error, then nothing when valid', async () => {
    const { fixture, host, text } = await render();
    host.nameForm.name().markAsTouched();

    host.model.set({ name: 'ab' });
    await fixture.whenStable();
    expect(text()).toBe('Use at least 3 characters.');

    host.model.set({ name: 'abc' });
    await fixture.whenStable();
    expect(text()).toBe('');
  });

  it('keeps the id on its host, for the input aria-describedby', async () => {
    const { element } = await render();

    expect(element.querySelector('#name-error')?.tagName.toLowerCase()).toBe(
      'app-form-field-error',
    );
  });

  it('is announced to screen readers when it appears', async () => {
    const { fixture, host, element } = await render();
    host.nameForm.name().markAsTouched();
    await fixture.whenStable();

    expect(element.querySelector('app-form-field-error [role="alert"]')?.textContent?.trim()).toBe(
      'Name is required.',
    );
  });
});
