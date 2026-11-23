import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  isDevMode,
  signal,
} from '@angular/core';
import { FormField, FormRoot, form } from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth-service';
import { FormFieldError } from '../../../shared/forms/form-field-error';
import { focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import { ToastOutlet } from '../../../shared/toast-outlet/toast-outlet';
import { AUTH_MESSAGES, LoginModel, loginSchema } from '../auth-schemas';
import { safeReturnUrl } from '../safe-return-url';

/** `/login` (AC2): signs in and goes back to where the user was going. */
@Component({
  selector: 'app-login',
  imports: [FormField, FormRoot, FormFieldError, RouterLink, ToastOutlet],
  templateUrl: './login.html',
  styleUrl: '../auth-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Login {
  readonly #auth = inject(AuthService);
  readonly #router = inject(Router);

  /** From the query string (withComponentInputBinding): where to go after signing in. */
  readonly returnUrl = input<string>();
  /** `expired` when the refresh interceptor sent the user here (T12). */
  readonly reason = input<string>();

  protected readonly messages = AUTH_MESSAGES;
  protected readonly isDev = isDevMode();
  protected readonly model = signal<LoginModel>({ userName: '', password: '' });
  protected readonly loginForm = form(this.model, loginSchema, {
    submission: {
      action: () => this.#signIn(),
      // The button stays enabled (a disabled one silently drops a fast Enter or an autofill submit):
      // an invalid submit shows every error and moves focus to the first field to fix.
      onInvalid: () => focusFirstInvalid([this.loginForm.userName, this.loginForm.password]),
    },
  });
  protected readonly pending = computed(() => this.loginForm().submitting());
  protected readonly serverError = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  async #signIn(): Promise<undefined> {
    this.serverError.set(null);
    const { userName, password } = this.model();

    try {
      await this.#auth.login(userName, password);
    } catch (error) {
      // One message for every credential failure: never reveal which field was wrong.
      // Network and server failures already show a toast (errorInterceptor).
      if (error instanceof HttpErrorResponse && (error.status === 401 || error.status === 400)) {
        this.serverError.set(AUTH_MESSAGES.invalidCredentials);
        this.loginForm.userName().focusBoundControl();
      }

      return undefined;
    }

    await this.#router.navigateByUrl(safeReturnUrl(this.returnUrl()));
    return undefined;
  }
}
