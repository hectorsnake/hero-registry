/** One page of a list, as every paged endpoint returns it (T10). */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** Number of pages (0 for an empty list). */
export function totalPages(result: Pick<PagedResult<unknown>, 'pageSize' | 'totalCount'>): number {
  return result.pageSize > 0 ? Math.ceil(result.totalCount / result.pageSize) : 0;
}
