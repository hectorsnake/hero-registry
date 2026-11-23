import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PowerMeter, SuperpowerChips } from '../hero-badges';
import { Hero } from '../hero-models';

/** One hero on small screens, with the same actions as the table. Presentational. */
@Component({
  selector: 'app-hero-card',
  imports: [RouterLink, PowerMeter, SuperpowerChips],
  template: `
    <article class="card" [attr.aria-labelledby]="'hero-card-title-' + hero().id">
      <h2 class="card__title" [id]="'hero-card-title-' + hero().id">{{ hero().heroName }}</h2>
      <p class="card__real-name">{{ hero().firstName }} {{ hero().lastName }}</p>
      <app-superpower-chips [superpowers]="hero().superpowers" />
      <app-power-meter [value]="hero().powerLevel" />
      <footer class="card__actions">
        <a
          class="icon-button"
          [routerLink]="['/heroes', hero().id]"
          [attr.aria-label]="'View ' + hero().heroName"
          [attr.title]="'View ' + hero().heroName"
          ><span aria-hidden="true">👁</span></a
        >
        <a
          class="icon-button"
          [routerLink]="['/heroes', hero().id, 'edit']"
          [attr.aria-label]="'Edit ' + hero().heroName"
          [attr.title]="'Edit ' + hero().heroName"
          ><span aria-hidden="true">✏️</span></a
        >
        @if (canDelete()) {
          <button
            type="button"
            class="icon-button"
            [attr.aria-label]="'Delete ' + hero().heroName"
            [attr.title]="'Delete ' + hero().heroName"
            (click)="deleteRequested.emit(hero())"
          >
            <span aria-hidden="true">🗑</span>
          </button>
        }
      </footer>
    </article>
  `,
  styles: `
    .card {
      display: grid;
      gap: var(--space-2);
      padding: var(--space-3);
      border: 1px solid var(--color-border);
      border-radius: var(--radius);
      background: var(--color-surface);
    }

    .card__title {
      margin: 0;
      font-size: 1.125rem;
    }

    .card__real-name {
      margin: 0;
      color: var(--color-text-muted);
    }

    .card__actions {
      display: flex;
      gap: var(--space-2);
      justify-content: flex-end;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeroCard {
  readonly hero = input.required<Hero>();
  readonly canDelete = input(false);
  readonly deleteRequested = output<Hero>();
}
