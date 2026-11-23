/** A hero as the API returns it (`HeroResponse`, T10). Dates are ISO strings. */
export interface Hero {
  id: number;
  heroName: string;
  firstName: string;
  lastName: string;
  /** `yyyy-MM-dd`. */
  birthDate: string;
  email: string;
  superpowers: string[];
  powerLevel: number;
  address1: string;
  address2: string | null;
  city: string;
  state: string;
  zipCode: string;
  createdBy: number | null;
  createdAt: string;
  updatedBy: number | null;
  updatedAt: string | null;
}

/** Body of `POST /api/heroes` and `PUT /api/heroes/{id}`. */
export interface HeroRequest {
  heroName: string;
  firstName: string;
  lastName: string;
  birthDate: string;
  email: string;
  superpowers: string[];
  powerLevel: number;
  address1: string;
  address2: string | null;
  city: string;
  state: string;
  zipCode: string;
}

/** The API's sortable columns (`HeroSortBy`). */
export type HeroSortBy = 'HeroName' | 'PowerLevel' | 'CreatedAt';

export type SortDirection = 'Ascending' | 'Descending';

export interface HeroSort {
  sortBy: HeroSortBy;
  sortDirection: SortDirection;
}

/** `GET /api/heroes` query string. */
export interface HeroQuery extends HeroSort {
  search?: string;
  page: number;
  pageSize: number;
}

export const DEFAULT_SORT: HeroSort = { sortBy: 'HeroName', sortDirection: 'Ascending' };

export const HERO_PAGE_SIZE = 10;
