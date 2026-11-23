import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { HeroApi, heroResource, heroesResource } from './hero-api';
import { Hero, HeroQuery, HeroRequest } from './hero-models';

function hero(overrides: Partial<Hero> = {}): Hero {
  return {
    id: 7,
    heroName: 'Superman',
    firstName: 'Clark',
    lastName: 'Kent',
    birthDate: '1978-06-18',
    email: 'clark@dailyplanet.com',
    superpowers: ['Flight'],
    powerLevel: 100,
    address1: '344 Clinton St',
    address2: null,
    city: 'Metropolis',
    state: 'NY',
    zipCode: '10001',
    createdBy: 1,
    createdAt: '2026-11-01T10:00:00Z',
    updatedBy: null,
    updatedAt: null,
    ...overrides,
  };
}

const QUERY: HeroQuery = { page: 1, pageSize: 10, sortBy: 'HeroName', sortDirection: 'Ascending' };

describe('HeroApi', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  const stable = () => TestBed.inject(ApplicationRef).whenStable();

  describe('heroesResource', () => {
    it('requests /api/heroes with the paging and sort parameters', async () => {
      const heroes = TestBed.runInInjectionContext(() => heroesResource(signal(QUERY)));
      TestBed.tick();

      const req = backend.expectOne((r) => r.url === '/api/heroes');
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('page')).toBe('1');
      expect(req.request.params.get('pageSize')).toBe('10');
      expect(req.request.params.get('sortBy')).toBe('HeroName');
      expect(req.request.params.get('sortDirection')).toBe('Ascending');
      expect(req.request.params.has('search')).toBe(false);
      req.flush({ items: [hero()], page: 1, pageSize: 10, totalCount: 1 });
      await stable();

      expect(heroes.value()?.items[0].heroName).toBe('Superman');
    });

    it('sends a trimmed search, and none when it is blank', () => {
      const query = signal<HeroQuery>({ ...QUERY, search: '  flight ' });
      TestBed.runInInjectionContext(() => heroesResource(query));
      TestBed.tick();
      expect(backend.expectOne((r) => r.url === '/api/heroes').request.params.get('search')).toBe(
        'flight',
      );

      query.set({ ...QUERY, search: '   ' });
      TestBed.tick();
      expect(backend.expectOne((r) => r.url === '/api/heroes').request.params.has('search')).toBe(
        false,
      );
    });

    it('refetches when the query signal changes', () => {
      const query = signal(QUERY);
      TestBed.runInInjectionContext(() => heroesResource(query));
      TestBed.tick();
      backend
        .expectOne((r) => r.url === '/api/heroes')
        .flush({ items: [], page: 1, pageSize: 10, totalCount: 0 });

      query.set({ ...QUERY, page: 2, sortBy: 'PowerLevel', sortDirection: 'Descending' });
      TestBed.tick();

      const req = backend.expectOne((r) => r.url === '/api/heroes');
      expect(req.request.params.get('page')).toBe('2');
      expect(req.request.params.get('sortBy')).toBe('PowerLevel');
      expect(req.request.params.get('sortDirection')).toBe('Descending');
    });
  });

  describe('heroResource', () => {
    it('loads GET /api/heroes/{id}', async () => {
      const resource = TestBed.runInInjectionContext(() => heroResource(signal(7)));
      TestBed.tick();

      backend.expectOne('/api/heroes/7').flush(hero());
      await stable();

      expect(resource.value()?.id).toBe(7);
    });

    it('makes no request while the id is undefined', () => {
      const resource = TestBed.runInInjectionContext(() =>
        heroResource(signal<number | undefined>(undefined)),
      );
      TestBed.tick();

      backend.expectNone(() => true);
      expect(resource.value()).toBeUndefined();
    });
  });

  describe('writes', () => {
    const request: HeroRequest = {
      heroName: 'Superman',
      firstName: 'Clark',
      lastName: 'Kent',
      birthDate: '1978-06-18',
      email: 'clark@dailyplanet.com',
      superpowers: ['Flight'],
      powerLevel: 100,
      address1: '344 Clinton St',
      address2: null,
      city: 'Metropolis',
      state: 'NY',
      zipCode: '10001',
    };

    it('create POSTs the request to /api/heroes', async () => {
      const created = firstValueFrom(TestBed.inject(HeroApi).create(request));

      const req = backend.expectOne('/api/heroes');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(request);
      req.flush(hero(), { status: 201, statusText: 'Created' });

      expect((await created).id).toBe(7);
    });

    it('update PUTs the request to /api/heroes/{id}', async () => {
      const updated = firstValueFrom(TestBed.inject(HeroApi).update(7, request));

      const req = backend.expectOne('/api/heroes/7');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(request);
      req.flush(hero());

      expect((await updated).heroName).toBe('Superman');
    });

    it('delete sends DELETE /api/heroes/{id}', async () => {
      const deleted = firstValueFrom(TestBed.inject(HeroApi).delete(7));

      const req = backend.expectOne('/api/heroes/7');
      expect(req.request.method).toBe('DELETE');
      req.flush(null, { status: 204, statusText: 'No Content' });

      await expect(deleted).resolves.toBeNull();
    });
  });
});
