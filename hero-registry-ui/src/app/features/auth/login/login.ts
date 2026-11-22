import { ChangeDetectionStrategy, Component } from '@angular/core';
import { ToastOutlet } from '../../../shared/toast-outlet/toast-outlet';

/** Placeholder: the sign-in and registration forms are built in T13 (Signal Forms). */
@Component({
  selector: 'app-login',
  imports: [ToastOutlet],
  template: `
    <main class="login">
      <h1>Sign in</h1>
      <p>The sign-in form is added in T13.</p>
    </main>
    <app-toast-outlet />
  `,
  styles: `
    .login {
      padding: var(--space-4);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Login {}
