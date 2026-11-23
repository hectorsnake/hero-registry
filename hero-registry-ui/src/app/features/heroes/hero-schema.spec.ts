import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ValidationError, form } from '@angular/forms/signals';
import { Hero } from './hero-models';
import {
  HERO_MESSAGES,
  HeroFormModel,
  US_STATES,
  emptyHero,
  heroSchema,
  toFormModel,
  toHeroRequest,
  todayIso,
} from './hero-schema';

// The "today" of every test: birth date rules are relative to it (UTC, like the server).
const NOW = new Date('2026-11-22T12:00:00Z');

function valid(overrides: Partial<HeroFormModel> = {}): HeroFormModel {
  return {
    heroName: 'Spider-Man',
    firstName: 'Peter',
    lastName: 'Parker',
    birthDate: '2001-08-10',
    email: 'peter@dailybugle.com',
    superpowers: ['Wall-crawling', 'Spider-sense'],
    powerLevel: 85,
    address1: '20 Ingram Street',
    address2: '',
    city: 'New York',
    state: 'NY',
    zipCode: '11375',
    ...overrides,
  };
}

function heroForm(overrides: Partial<HeroFormModel> = {}) {
  return TestBed.runInInjectionContext(() => form(signal(valid(overrides)), heroSchema));
}

function kinds(errors: readonly ValidationError[]): string[] {
  return errors.map((e) => e.kind);
}

describe('heroSchema', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(NOW);
  });

  afterEach(() => vi.useRealTimers());

  it('accepts a complete, valid hero', () => {
    expect(heroForm()().valid()).toBe(true);
  });

  describe('heroName', () => {
    it.each([
      ['', ['required']],
      ['A', ['minLength']], // 1
      ['Ab', []], // 2: the minimum
      ['a'.repeat(100), []], // 100: the maximum
      ['a'.repeat(101), ['maxLength']], // 101
    ])('"%s" → %j', (heroName, expected) => {
      expect(kinds(heroForm({ heroName }).heroName().errors())).toEqual(expected);
    });
  });

  describe('firstName and lastName', () => {
    it('are required', () => {
      const f = heroForm({ firstName: '', lastName: '' });

      expect(kinds(f.firstName().errors())).toEqual(['required']);
      expect(kinds(f.lastName().errors())).toEqual(['required']);
    });

    it('allow at most 100 characters', () => {
      expect(
        kinds(
          heroForm({ firstName: 'a'.repeat(101) })
            .firstName()
            .errors(),
        ),
      ).toEqual(['maxLength']);
      expect(
        heroForm({ lastName: 'a'.repeat(100) })
          .lastName()
          .errors(),
      ).toEqual([]);
    });
  });

  describe('birthDate', () => {
    it.each([
      ['', ['required']],
      ['2026-11-22', ['birthDate.past']], // today
      ['2027-01-01', ['birthDate.past']], // future
      ['2026-11-21', []], // yesterday
      ['1876-11-22', []], // exactly 150 years ago
      ['1876-11-21', ['birthDate.tooOld']], // more than 150 years ago
    ])('"%s" → %j', (birthDate, expected) => {
      expect(kinds(heroForm({ birthDate }).birthDate().errors())).toEqual(expected);
    });

    it('exposes today (UTC) for the date picker max', () => {
      expect(todayIso()).toBe('2026-11-22');
    });
  });

  describe('email', () => {
    it.each([
      ['', ['required']],
      ['not-an-email', ['email']],
    ])('"%s" → %j', (email, expected) => {
      expect(kinds(heroForm({ email }).email().errors())).toEqual(expected);
    });

    it('allows at most 256 characters', () => {
      const tooLong = `${'a'.repeat(245)}@example.com`; // 257 characters

      expect(kinds(heroForm({ email: tooLong }).email().errors())).toContain('maxLength');
    });
  });

  describe('superpowers', () => {
    it('needs at least one', () => {
      const errors = heroForm({ superpowers: [] }).superpowers().errors();

      expect(kinds(errors)).toEqual(['superpowers.count']);
      expect(errors[0].message).toBe(HERO_MESSAGES.superpowersCount);
    });

    it('allows 10 but not 11', () => {
      const ten = Array.from({ length: 10 }, (_, i) => `Power ${i}`);

      expect(heroForm({ superpowers: ten }).superpowers().errors()).toEqual([]);
      expect(
        kinds(
          heroForm({ superpowers: [...ten, 'Power 10'] })
            .superpowers()
            .errors(),
        ),
      ).toEqual(['superpowers.count']);
    });

    it('rejects duplicates, ignoring case and spaces', () => {
      expect(
        kinds(
          heroForm({ superpowers: ['Flight', ' flight '] })
            .superpowers()
            .errors(),
        ),
      ).toEqual(['superpowers.duplicate']);
    });

    it.each([
      [['F'], ['superpowers.itemLength']], // 1 character
      [['Fl'], []], // 2
      [['a'.repeat(50)], []], // 50
      [['a'.repeat(51)], ['superpowers.itemLength']], // 51
    ])('item length: %j → %j', (superpowers, expected) => {
      expect(kinds(heroForm({ superpowers }).superpowers().errors())).toEqual(expected);
    });
  });

  describe('powerLevel', () => {
    it.each([
      [0, ['min']],
      [1, []],
      [100, []],
      [101, ['max']],
    ])('%i → %j', (powerLevel, expected) => {
      expect(kinds(heroForm({ powerLevel }).powerLevel().errors())).toEqual(expected);
    });

    it('is required (an emptied number input gives NaN)', () => {
      expect(kinds(heroForm({ powerLevel: Number.NaN }).powerLevel().errors())).toContain(
        'required',
      );
    });
  });

  describe('address', () => {
    it('address1 and city are required; address2 is optional', () => {
      const f = heroForm({ address1: '', city: '', address2: '' });

      expect(kinds(f.address1().errors())).toEqual(['required']);
      expect(kinds(f.city().errors())).toEqual(['required']);
      expect(f.address2().errors()).toEqual([]);
    });

    it('enforces the maximum lengths', () => {
      const f = heroForm({
        address1: 'a'.repeat(201),
        address2: 'a'.repeat(201),
        city: 'a'.repeat(101),
      });

      expect(kinds(f.address1().errors())).toEqual(['maxLength']);
      expect(kinds(f.address2().errors())).toEqual(['maxLength']);
      expect(kinds(f.city().errors())).toEqual(['maxLength']);
    });
  });

  describe('state', () => {
    it.each(['NY', 'DC', 'PR'])('accepts %s', (state) => {
      expect(heroForm({ state }).state().errors()).toEqual([]);
    });

    it.each([
      ['', ['required']],
      ['XX', ['state.invalid']],
      ['New York', ['state.invalid']],
    ])('"%s" → %j', (state, expected) => {
      expect(kinds(heroForm({ state }).state().errors())).toEqual(expected);
    });

    it('offers the same codes as the server, with names for the select', () => {
      expect(US_STATES).toHaveLength(56); // 50 states, DC and 5 territories
      expect(US_STATES.find((s) => s.code === 'NY')?.name).toBe('New York');
    });
  });

  describe('zipCode', () => {
    it.each([
      ['11375', []],
      ['11375-1234', []],
      ['', ['required']],
      ['1137', ['pattern']],
      ['11375-12', ['pattern']],
      ['١١٣٧٥', ['pattern']], // non-ASCII digits, rejected like the server
    ])('"%s" → %j', (zipCode, expected) => {
      expect(kinds(heroForm({ zipCode }).zipCode().errors())).toEqual(expected);
    });
  });

  it('every error carries a message for the field', () => {
    const f = heroForm({ heroName: '', zipCode: 'x', state: 'XX', superpowers: [] });

    for (const field of [f.heroName, f.zipCode, f.state, f.superpowers]) {
      expect(
        field()
          .errors()
          .every((e) => !!e.message),
      ).toBe(true);
    }
  });
});

describe('form model mapping', () => {
  const hero: Hero = {
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
  };

  it('emptyHero starts blank, with a mid power level', () => {
    expect(emptyHero()).toEqual({
      heroName: '',
      firstName: '',
      lastName: '',
      birthDate: '',
      email: '',
      superpowers: [],
      powerLevel: 50,
      address1: '',
      address2: '',
      city: '',
      state: '',
      zipCode: '',
    });
  });

  it('toFormModel copies a loaded hero (null address2 → empty)', () => {
    expect(toFormModel(hero)).toEqual({
      heroName: 'Superman',
      firstName: 'Clark',
      lastName: 'Kent',
      birthDate: '1978-06-18',
      email: 'clark@dailyplanet.com',
      superpowers: ['Flight'],
      powerLevel: 100,
      address1: '344 Clinton St',
      address2: '',
      city: 'Metropolis',
      state: 'NY',
      zipCode: '10001',
    });
  });

  it('toFormModel gives undefined while nothing is loaded', () => {
    expect(toFormModel(undefined)).toBeUndefined();
  });

  it('toHeroRequest trims every text, upper-cases the state and sends an empty address2 as null', () => {
    const request = toHeroRequest(
      valid({
        heroName: '  Spider-Man ',
        email: ' Peter@DailyBugle.com ',
        superpowers: [' Agility ', 'Spider-sense'],
        address2: '   ',
        state: 'ny',
        zipCode: ' 11375 ',
      }),
    );

    expect(request).toEqual({
      heroName: 'Spider-Man',
      firstName: 'Peter',
      lastName: 'Parker',
      birthDate: '2001-08-10',
      email: 'Peter@DailyBugle.com',
      superpowers: ['Agility', 'Spider-sense'],
      powerLevel: 85,
      address1: '20 Ingram Street',
      address2: null,
      city: 'New York',
      state: 'NY',
      zipCode: '11375',
    });
  });
});
