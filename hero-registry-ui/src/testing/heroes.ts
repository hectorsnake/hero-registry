import { Hero } from '../app/features/heroes/hero-models';

/** A valid hero for specs; override only what the test is about. */
export function hero(overrides: Partial<Hero> = {}): Hero {
  return {
    id: 7,
    heroName: 'Superman',
    firstName: 'Clark',
    lastName: 'Kent',
    birthDate: '1978-06-18',
    email: 'clark@dailyplanet.com',
    superpowers: ['Flight', 'Super strength', 'Heat vision', 'X-ray vision'],
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
