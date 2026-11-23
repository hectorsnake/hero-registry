import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';

/** Previous / "Page X of Y" / Next. Two-way bind `[(page)]`; hidden when there is a single page. */
@Component({
  selector: 'app-paginator',
  template: `
    @if (totalPages() > 1) {
      <nav class="paginator" aria-label="Pagination">
        <button
          type="button"
          class="paginator__button"
          aria-label="Previous page"
          [disabled]="page() <= 1"
          (click)="page.set(page() - 1)"
        >
          ‹ Prev
        </button>
        <span class="paginator__status" aria-current="page"
          >Page {{ page() }} of {{ totalPages() }}</span
        >
        <button
          type="button"
          class="paginator__button"
          aria-label="Next page"
          [disabled]="page() >= totalPages()"
          (click)="page.set(page() + 1)"
        >
          Next ›
        </button>
      </nav>
    }
  `,
  styles: `
    .paginator {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-3);
      align-items: center;
      justify-content: flex-end;
    }

    .paginator__button {
      padding: var(--space-1) var(--space-3);
      border: 1px solid var(--color-border);
      border-radius: var(--radius);
      background: var(--color-surface);
      color: var(--color-text);
      font: inherit;
      cursor: pointer;

      &:disabled {
        opacity: 0.5;
        cursor: not-allowed;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Paginator {
  readonly page = model.required<number>();
  readonly totalPages = input.required<number>();
  readonly pageSize = input<number>();
}
