import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { RelativeTimePipe } from '../../../shared/relative-time/relative-time';
import { PowerMeter, SuperpowerChips } from '../hero-badges';
import { DEFAULT_SORT, Hero, HeroSort, HeroSortBy } from '../hero-models';

/** The next sort after clicking a column: a new column sorts ascending, the same one toggles. */
export function nextSort(current: HeroSort, column: HeroSortBy): HeroSort {
  if (current.sortBy !== column) {
    return { sortBy: column, sortDirection: 'Ascending' };
  }

  return {
    sortBy: column,
    sortDirection: current.sortDirection === 'Ascending' ? 'Descending' : 'Ascending',
  };
}

/** The heroes list on wide screens. Presentational: the list page owns the data, the URL and deletes. */
@Component({
  selector: 'app-hero-table',
  imports: [RouterLink, RelativeTimePipe, PowerMeter, SuperpowerChips],
  templateUrl: './hero-table.html',
  styleUrl: './hero-table.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeroTable {
  readonly heroes = input.required<readonly Hero[]>();
  readonly canDelete = input(false);
  readonly sort = input<HeroSort>(DEFAULT_SORT);
  readonly sortChange = output<HeroSort>();
  readonly deleteRequested = output<Hero>();

  protected sortBy(column: HeroSortBy): void {
    this.sortChange.emit(nextSort(this.sort(), column));
  }

  /** `aria-sort` for a column header, or null (attribute removed) when it is not the sorted one. */
  protected ariaSort(column: HeroSortBy): 'ascending' | 'descending' | null {
    const sort = this.sort();
    if (sort.sortBy !== column) {
      return null;
    }

    return sort.sortDirection === 'Ascending' ? 'ascending' : 'descending';
  }
}
