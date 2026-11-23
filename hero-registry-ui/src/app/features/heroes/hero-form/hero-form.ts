import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  numberAttribute,
  signal,
} from '@angular/core';
import { FieldTree, FormField, FormRoot, ValidationError, form } from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { problemTitle, toFieldErrors } from '../../../core/api/problem-details';
import { ToastService } from '../../../core/ui/toast-service';
import { ChipsInput } from '../../../shared/chips-input/chips-input';
import { focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import { FormFieldError } from '../../../shared/forms/form-field-error';
import { HasUnsavedChanges } from '../../../shared/forms/unsaved-changes-guard';
import { HeroApi, heroResource } from '../hero-api';
import { HeroListMemory } from '../hero-list-memory';
import {
  HeroFormModel,
  US_STATES,
  emptyHero,
  heroSchema,
  toFormModel,
  toHeroRequest,
  todayIso,
} from '../hero-schema';

/** The form's fields in visual order: focus order after a failed submit, and the server error keys. */
const FIELDS = [
  'heroName',
  'firstName',
  'lastName',
  'birthDate',
  'email',
  'superpowers',
  'powerLevel',
  'address1',
  'address2',
  'city',
  'state',
  'zipCode',
] as const satisfies readonly (keyof HeroFormModel)[];

/** `/heroes/new` and `/heroes/:id/edit` (AC4, AC7): create or edit a hero with Signal Forms. */
@Component({
  selector: 'app-hero-form',
  imports: [FormField, FormRoot, FormFieldError, ChipsInput, RouterLink],
  templateUrl: './hero-form.html',
  styleUrl: './hero-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeroForm implements HasUnsavedChanges {
  readonly #api = inject(HeroApi);
  readonly #router = inject(Router);
  readonly #toasts = inject(ToastService);
  readonly #listQuery = inject(HeroListMemory).queryParams;

  /** From the route in edit mode (`/heroes/:id/edit`); undefined when creating. */
  readonly id = input<number | undefined, unknown>(undefined, {
    transform: (value: unknown) =>
      value === undefined || value === null || value === '' ? undefined : numberAttribute(value),
  });

  protected readonly isEdit = computed(() => this.id() !== undefined);
  protected readonly existing = heroResource(this.id);
  /** The loaded hero; `value()` throws while the resource is in its error state, so check first. */
  readonly #loaded = computed(() => (this.existing.hasValue() ? this.existing.value() : undefined));
  protected readonly notFound = computed(() => {
    const error = this.existing.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });
  protected readonly title = computed(() =>
    this.isEdit() ? `Edit ${this.#loaded()?.heroName ?? 'hero'}` : 'Register a hero',
  );

  /** Empty when creating; the loaded hero when editing (no patchValue, no effect). */
  protected readonly model = linkedSignal<HeroFormModel>(
    () => toFormModel(this.#loaded()) ?? emptyHero(),
  );
  protected readonly heroForm = form(this.model, heroSchema, {
    submission: {
      action: () => this.#save(),
      onInvalid: () => focusFirstInvalid(this.#fieldsInOrder()),
    },
  });
  protected readonly pending = computed(() => this.heroForm().submitting());
  protected readonly serverError = signal<string | null>(null);
  protected readonly saved = signal(false);

  protected readonly states = US_STATES;
  protected readonly today = todayIso();

  hasUnsavedChanges(): boolean {
    return this.heroForm().dirty() && !this.saved();
  }

  protected cancel(): void {
    const id = this.id();
    void (id === undefined
      ? this.#router.navigate(['/heroes'], { queryParams: this.#listQuery() })
      : this.#router.navigate(['/heroes', id]));
  }

  async #save(): Promise<ValidationError.WithOptionalFieldTree[] | undefined> {
    this.serverError.set(null);
    const request = toHeroRequest(this.model());
    const id = this.id();

    try {
      const hero = await firstValueFrom(
        id === undefined ? this.#api.create(request) : this.#api.update(id, request),
      );
      this.saved.set(true);
      this.#toasts.show('Hero saved.', 'success');
      await this.#router.navigate(['/heroes', hero.id]);
      return undefined;
    } catch (error) {
      return this.#serverErrors(error);
    }
  }

  /** 400 and 409 go next to their fields (and focus the first); other problems show their title. */
  #serverErrors(error: unknown): ValidationError.WithOptionalFieldTree[] | undefined {
    if (!(error instanceof HttpErrorResponse) || error.status === 0 || error.status >= 500) {
      return undefined; // errorInterceptor already showed a toast
    }

    if (error.status === 404) {
      this.serverError.set('This hero no longer exists. It may have been deleted.');
      return undefined;
    }

    const byField = toFieldErrors(error, FIELDS);
    const fields = FIELDS.filter((field) => byField[field]?.length);
    if (fields.length === 0) {
      this.serverError.set(problemTitle(error, 'The hero could not be saved. Please try again.'));
      return undefined;
    }

    this.heroForm[fields[0]]().focusBoundControl();
    return fields.flatMap((field) =>
      (byField[field] ?? []).map((message) => ({
        kind: 'server',
        message,
        fieldTree: this.heroForm[field],
      })),
    );
  }

  #fieldsInOrder(): FieldTree<unknown>[] {
    return FIELDS.map((field) => this.heroForm[field] as FieldTree<unknown>);
  }
}
