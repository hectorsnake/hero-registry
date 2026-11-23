import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  numberAttribute,
  signal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../../core/auth/auth-service';
import { ToastService } from '../../../core/ui/toast-service';
import { ConfirmDialog } from '../../../shared/confirm-dialog/confirm-dialog';
import { RelativeTimePipe } from '../../../shared/relative-time/relative-time';
import { HeroApi, heroResource } from '../hero-api';
import { PowerMeter, SuperpowerChips } from '../hero-badges';
import { HeroListMemory } from '../hero-list-memory';

/** `/heroes/:id` (AC5): one hero, read-only, with its audit trail; Edit for all, Delete for admins. */
@Component({
  selector: 'app-hero-detail',
  imports: [RouterLink, ConfirmDialog, RelativeTimePipe, PowerMeter, SuperpowerChips],
  templateUrl: './hero-detail.html',
  styleUrl: './hero-detail.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeroDetail {
  readonly #api = inject(HeroApi);
  readonly #router = inject(Router);
  readonly #toasts = inject(ToastService);
  protected readonly auth = inject(AuthService);
  /** The list query to go back to (search, sort and page). */
  protected readonly listQuery = inject(HeroListMemory).queryParams;

  /** From the route (`/heroes/:id`), as a number. */
  readonly id = input.required<number, unknown>({ transform: numberAttribute });

  protected readonly hero = heroResource(this.id);
  protected readonly notFound = computed(() => {
    const error = this.hero.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });

  protected readonly confirmOpen = signal(false);
  protected readonly deleting = signal(false);

  protected async confirmDelete(): Promise<void> {
    const hero = this.hero.value();
    if (!hero || this.deleting()) {
      return;
    }

    this.deleting.set(true);
    try {
      await firstValueFrom(this.#api.delete(hero.id));
      this.#toasts.show('Hero deleted.', 'success');
      this.confirmOpen.set(false);
      await this.#router.navigate(['/heroes'], { queryParams: this.listQuery() });
    } catch (error) {
      this.confirmOpen.set(false);
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.hero.reload(); // already deleted: show the not-found panel
      }
    } finally {
      this.deleting.set(false);
    }
  }
}
