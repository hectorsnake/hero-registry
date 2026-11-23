import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormField, FormRoot, ValidationError, form } from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';
import { problemTitle, toFieldErrors } from '../../../core/api/problem-details';
import { AuthService } from '../../../core/auth/auth-service';
import { FormFieldError } from '../../../shared/forms/form-field-error';
import { focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import { ToastOutlet } from '../../../shared/toast-outlet/toast-outlet';
import { RegisterModel, passwordChecklist, registerSchema } from '../auth-schemas';
import { DEFAULT_AFTER_LOGIN } from '../safe-return-url';

/** The fields the API can report errors on (camelCase, as in the problem's `errors`). */
const SERVER_FIELDS = ['userName', 'email', 'password'] as const;
type ServerField = (typeof SERVER_FIELDS)[number];

/** `/register` (AC1): creates a regular account, signs in and opens the heroes list. */
@Component({
  selector: 'app-register',
  imports: [FormField, FormRoot, FormFieldError, RouterLink, ToastOutlet],
  templateUrl: './register.html',
  styleUrl: '../auth-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Register {
  readonly #auth = inject(AuthService);
  readonly #router = inject(Router);

  protected readonly model = signal<RegisterModel>({
    userName: '',
    email: '',
    password: '',
    confirmPassword: '',
  });
  protected readonly registerForm = form(this.model, registerSchema, {
    submission: {
      action: () => this.#register(),
      // The button stays enabled (a disabled one silently drops a fast Enter or an autofill submit):
      // an invalid submit shows every error and moves focus to the first field to fix.
      onInvalid: () =>
        focusFirstInvalid([
          this.registerForm.userName,
          this.registerForm.email,
          this.registerForm.password,
          this.registerForm.confirmPassword,
        ]),
    },
  });
  protected readonly pending = computed(() => this.registerForm().submitting());
  protected readonly serverError = signal<string | null>(null);
  protected readonly showPassword = signal(false);
  /** ✓ or ✗ per password rule, live while typing. */
  protected readonly checklist = computed(() =>
    passwordChecklist(this.registerForm.password().errors()),
  );

  async #register(): Promise<ValidationError.WithOptionalFieldTree[] | undefined> {
    this.serverError.set(null);
    const { userName, email, password } = this.model();

    try {
      await this.#auth.register({ userName, email, password });
    } catch (error) {
      return this.#serverErrors(error);
    }

    await this.#router.navigateByUrl(DEFAULT_AFTER_LOGIN);
    return undefined;
  }

  /** 400 and 409 go next to their fields; other problems show their title. */
  #serverErrors(error: unknown): ValidationError.WithOptionalFieldTree[] | undefined {
    if (!(error instanceof HttpErrorResponse) || error.status === 0 || error.status >= 500) {
      return undefined; // errorInterceptor already showed a toast
    }

    const byField = toFieldErrors(error, SERVER_FIELDS);
    const fields = SERVER_FIELDS.filter((field) => byField[field]?.length);
    if (fields.length === 0) {
      this.serverError.set(
        problemTitle(error, 'Your account could not be created. Please try again.'),
      );
      return undefined;
    }

    this.registerForm[fields[0]]().focusBoundControl();
    return fields.flatMap((field: ServerField) =>
      (byField[field] ?? []).map((message) => ({
        kind: 'server',
        message,
        fieldTree: this.registerForm[field],
      })),
    );
  }
}
