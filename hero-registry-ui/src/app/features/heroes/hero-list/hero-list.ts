import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  numberAttribute,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Params, Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, firstValueFrom } from 'rxjs';
import { totalPages } from '../../../core/api/paged-result';
import { AuthService } from '../../../core/auth/auth-service';
import { ToastService } from '../../../core/ui/toast-service';
import { ViewportService } from '../../../core/ui/viewport-service';
import { ConfirmDialog } from '../../../shared/confirm-dialog/confirm-dialog';
import { Paginator } from '../../../shared/paginator/paginator';
import { HeroApi, heroesResource } from '../hero-api';
import { HeroCard } from '../hero-card/hero-card';
import { HeroListMemory } from '../hero-list-memory';
import {
  DEFAULT_SORT,
  HERO_PAGE_SIZE,
  Hero,
  HeroQuery,
  HeroSort,
  HeroSortBy,
  SortDirection,
} from '../hero-models';
import { HeroTable } from '../hero-table/hero-table';

const SORT_COLUMNS: readonly HeroSortBy[] = ['HeroName', 'PowerLevel', 'CreatedAt'];
const SORT_DIRECTIONS: readonly SortDirection[] = ['Ascending', 'Descending'];
export const SEARCH_DEBOUNCE_MS = 300;

/**
 * `/heroes` (AC5, AC6, AC8, AC9). The URL is the source of truth for search, sort and page, so the list
 * is shareable and the back button works; the query-string values arrive as signal inputs.
 */
@Component({
  selector: 'app-hero-list',
  imports: [RouterLink, HeroTable, HeroCard, Paginator, ConfirmDialog],
  templateUrl: './hero-list.html',
  styleUrl: './hero-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeroList {
  readonly #router = inject(Router);
  readonly #route = inject(ActivatedRoute);
  readonly #api = inject(HeroApi);
  readonly #toasts = inject(ToastService);
  readonly #injector = inject(Injector);
  protected readonly auth = inject(AuthService);
  protected readonly viewport = inject(ViewportService);

  // Query-string inputs (withComponentInputBinding).
  readonly search = input<string>();
  readonly page = input(1, {
    transform: (value: unknown) => Math.max(1, numberAttribute(value, 1)),
  });
  readonly sortBy = input<string>();
  readonly sortDirection = input<string>();

  protected readonly sort = computed<HeroSort>(() => ({
    sortBy: SORT_COLUMNS.find((c) => c === this.sortBy()) ?? DEFAULT_SORT.sortBy,
    sortDirection:
      SORT_DIRECTIONS.find((d) => d === this.sortDirection()) ?? DEFAULT_SORT.sortDirection,
  }));
  protected readonly query = computed<HeroQuery>(() => ({
    search: this.search()?.trim() || undefined,
    page: this.page(),
    pageSize: HERO_PAGE_SIZE,
    ...this.sort(),
  }));
  protected readonly heroes = heroesResource(this.query);

  /** The loaded page; `value()` throws while the resource is in its error state, so check first. */
  readonly #result = computed(() => (this.heroes.hasValue() ? this.heroes.value() : undefined));
  protected readonly items = computed(() => this.#result()?.items ?? []);
  protected readonly totalCount = computed(() => this.#result()?.totalCount ?? 0);
  protected readonly totalPages = computed(() => {
    const result = this.#result();
    return result ? totalPages(result) : 0;
  });
  protected readonly countText = computed(() => {
    const count = this.totalCount();
    return `${count} ${count === 1 ? 'hero' : 'heroes'} found`;
  });

  /** What the user is typing; follows the URL when it changes (back button, Clear search). */
  protected readonly searchText = linkedSignal(() => this.search() ?? '');
  readonly #debouncedSearch = toSignal(
    toObservable(this.searchText).pipe(debounceTime(SEARCH_DEBOUNCE_MS), distinctUntilChanged()),
  );

  protected readonly heroToDelete = signal<Hero | null>(null);
  protected readonly confirmOpen = signal(false);
  protected readonly deleting = signal(false);
  protected readonly skeletonRows = [1, 2, 3, 4, 5];
  private readonly heading = viewChild.required<ElementRef<HTMLHeadingElement>>('heading');

  constructor() {
    const memory = inject(HeroListMemory);

    // Writes the debounced search to the URL (a navigation is a side effect outside the signal graph).
    effect(() => {
      const term = this.#debouncedSearch()?.trim();
      if (term !== undefined && term !== (untracked(this.search) ?? '').trim()) {
        this.#navigate({ search: term || null, page: null });
      }
    });

    // Lets the detail and form pages link back to this exact list.
    effect(() => {
      const { search, page, sortBy, sortDirection } = this.query();
      memory.remember({ ...(search ? { search } : {}), page, sortBy, sortDirection });
    });
  }

  protected clearSearch(): void {
    this.searchText.set('');
    this.#navigate({ search: null, page: null });
  }

  protected changeSort(sort: HeroSort): void {
    this.#navigate({ ...sort, page: null });
  }

  protected goToPage(page: number): void {
    this.#navigate({ page });
  }

  protected askDelete(hero: Hero): void {
    this.heroToDelete.set(hero);
    this.confirmOpen.set(true);
  }

  protected async confirmDelete(): Promise<void> {
    const hero = this.heroToDelete();
    if (!hero || this.deleting()) {
      return;
    }

    this.deleting.set(true);
    try {
      await firstValueFrom(this.#api.delete(hero.id));
      this.#toasts.show('Hero deleted.', 'success');
      this.#afterDelete();
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.#toasts.show('That hero was already deleted.', 'info');
        this.heroes.reload();
      }
      // A 403 or a server error already shows a toast (errorInterceptor).
    } finally {
      this.deleting.set(false);
      this.confirmOpen.set(false);
      this.heroToDelete.set(null);
      // After the render that closes the modal dialog: while it is open, the page is inert.
      afterNextRender(() => this.heading().nativeElement.focus(), { injector: this.#injector });
    }
  }

  /** The page now has one hero fewer: if it was the last one, show the previous page. */
  #afterDelete(): void {
    if (this.items().length === 1 && this.page() > 1) {
      this.#navigate({ page: this.page() - 1 });
    } else {
      this.heroes.reload();
    }
  }

  #navigate(queryParams: Params): void {
    void this.#router.navigate([], {
      relativeTo: this.#route,
      queryParams,
      queryParamsHandling: 'merge',
    });
  }
}
