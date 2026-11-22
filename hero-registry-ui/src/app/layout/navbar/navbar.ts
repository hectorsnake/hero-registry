import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../core/auth/auth-service';
import { profileResource } from '../../core/auth/profile-resource';
import { ViewportService } from '../../core/ui/viewport-service';

/** Top bar: app name, navigation, the signed-in user (admin badge, email) and logout. */
@Component({
  selector: 'app-navbar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './navbar.html',
  styleUrl: './navbar.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Navbar {
  protected readonly auth = inject(AuthService);
  readonly #viewport = inject(ViewportService);

  readonly appName = input.required<string>();

  protected readonly isMobile = this.#viewport.isMobile;
  protected readonly menuOpen = signal(false);
  /** Always visible on a desktop; behind the toggle on mobile. */
  protected readonly menuVisible = computed(() => !this.isMobile() || this.menuOpen());
  protected readonly profile = profileResource(this.auth.isAuthenticated);

  protected toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  protected logout(): void {
    this.menuOpen.set(false);
    void this.auth.logout();
  }
}
