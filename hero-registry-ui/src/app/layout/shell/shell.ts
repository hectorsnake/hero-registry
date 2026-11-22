import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastOutlet } from '../../shared/toast-outlet/toast-outlet';
import { Navbar } from '../navbar/navbar';

/** The name shown in the navbar. */
export const APP_NAME = 'SuperHero Registry';

/**
 * The signed-in layout: navbar, the routed page and the toasts. It takes no inputs on purpose: it is a
 * routed component, and withComponentInputBinding() would reset them to undefined (no such route param).
 */
@Component({
  selector: 'app-shell',
  imports: [Navbar, RouterOutlet, ToastOutlet],
  template: `
    <app-navbar [appName]="appName" />
    <main class="shell__main"><router-outlet /></main>
    <app-toast-outlet />
  `,
  styles: `
    :host {
      display: grid;
      grid-template-rows: auto 1fr;
      min-block-size: 100dvh;
    }

    .shell__main {
      inline-size: min(100%, 72rem);
      margin-inline: auto;
      padding: var(--space-4);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Shell {
  protected readonly appName = APP_NAME;
}
