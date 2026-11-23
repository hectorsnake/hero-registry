import {
  ValidationError,
  email,
  max,
  maxLength,
  min,
  minLength,
  pattern,
  required,
  schema,
  validate,
} from '@angular/forms/signals';
import { Hero, HeroRequest } from './hero-models';

/** The editable hero, as the form holds it. Optional texts are '' rather than null. */
export interface HeroFormModel {
  heroName: string;
  firstName: string;
  lastName: string;
  /** `yyyy-MM-dd` from `<input type="date">`, or ''. */
  birthDate: string;
  email: string;
  superpowers: string[];
  powerLevel: number;
  address1: string;
  address2: string;
  city: string;
  state: string;
  zipCode: string;
}

/** Limits from the server's SuperHeroRules (T04/T07). */
export const HERO_RULES = {
  heroNameMin: 2,
  heroNameMax: 100,
  personNameMax: 100,
  emailMax: 256,
  superpowersMin: 1,
  superpowersMax: 10,
  superpowerMin: 2,
  superpowerMax: 50,
  powerMin: 1,
  powerMax: 100,
  addressMax: 200,
  cityMax: 100,
  maxAgeYears: 150,
} as const;

/** Every message the hero form shows, in one place. */
export const HERO_MESSAGES = {
  required: 'This field is required.',
  heroNameLength: 'Use 2 to 100 characters.',
  nameLength: 'Use at most 100 characters.',
  birthDatePast: 'The birth date must be in the past.',
  birthDateTooOld: 'The birth date cannot be more than 150 years ago.',
  email: 'Enter a valid email address.',
  emailLength: 'Use at most 256 characters.',
  superpowersCount: 'Add between 1 and 10 superpowers.',
  superpowersDuplicate: 'Each superpower can only be listed once.',
  superpowerLength: 'Each superpower must be 2 to 50 characters.',
  powerLevel: 'Choose a power level from 1 to 100.',
  addressLength: 'Use at most 200 characters.',
  cityLength: 'Use at most 100 characters.',
  state: 'Choose a US state.',
  zipCode: 'Use a 5-digit ZIP code, optionally followed by -1234.',
} as const;

/** The codes the server accepts (UsStateCode): 50 states, DC and 5 territories, for the select. */
export const US_STATES: readonly { code: string; name: string }[] = [
  { code: 'AL', name: 'Alabama' },
  { code: 'AK', name: 'Alaska' },
  { code: 'AZ', name: 'Arizona' },
  { code: 'AR', name: 'Arkansas' },
  { code: 'CA', name: 'California' },
  { code: 'CO', name: 'Colorado' },
  { code: 'CT', name: 'Connecticut' },
  { code: 'DE', name: 'Delaware' },
  { code: 'FL', name: 'Florida' },
  { code: 'GA', name: 'Georgia' },
  { code: 'HI', name: 'Hawaii' },
  { code: 'ID', name: 'Idaho' },
  { code: 'IL', name: 'Illinois' },
  { code: 'IN', name: 'Indiana' },
  { code: 'IA', name: 'Iowa' },
  { code: 'KS', name: 'Kansas' },
  { code: 'KY', name: 'Kentucky' },
  { code: 'LA', name: 'Louisiana' },
  { code: 'ME', name: 'Maine' },
  { code: 'MD', name: 'Maryland' },
  { code: 'MA', name: 'Massachusetts' },
  { code: 'MI', name: 'Michigan' },
  { code: 'MN', name: 'Minnesota' },
  { code: 'MS', name: 'Mississippi' },
  { code: 'MO', name: 'Missouri' },
  { code: 'MT', name: 'Montana' },
  { code: 'NE', name: 'Nebraska' },
  { code: 'NV', name: 'Nevada' },
  { code: 'NH', name: 'New Hampshire' },
  { code: 'NJ', name: 'New Jersey' },
  { code: 'NM', name: 'New Mexico' },
  { code: 'NY', name: 'New York' },
  { code: 'NC', name: 'North Carolina' },
  { code: 'ND', name: 'North Dakota' },
  { code: 'OH', name: 'Ohio' },
  { code: 'OK', name: 'Oklahoma' },
  { code: 'OR', name: 'Oregon' },
  { code: 'PA', name: 'Pennsylvania' },
  { code: 'RI', name: 'Rhode Island' },
  { code: 'SC', name: 'South Carolina' },
  { code: 'SD', name: 'South Dakota' },
  { code: 'TN', name: 'Tennessee' },
  { code: 'TX', name: 'Texas' },
  { code: 'UT', name: 'Utah' },
  { code: 'VT', name: 'Vermont' },
  { code: 'VA', name: 'Virginia' },
  { code: 'WA', name: 'Washington' },
  { code: 'WV', name: 'West Virginia' },
  { code: 'WI', name: 'Wisconsin' },
  { code: 'WY', name: 'Wyoming' },
  { code: 'DC', name: 'District of Columbia' },
  { code: 'AS', name: 'American Samoa' },
  { code: 'GU', name: 'Guam' },
  { code: 'MP', name: 'Northern Mariana Islands' },
  { code: 'PR', name: 'Puerto Rico' },
  { code: 'VI', name: 'U.S. Virgin Islands' },
];

const STATE_CODES = new Set(US_STATES.map((s) => s.code));

/** Today as `yyyy-MM-dd` in UTC, like the server's birth date rules (also the date picker's max). */
export function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

function yearsBefore(isoDate: string, years: number): string {
  const date = new Date(`${isoDate}T00:00:00Z`);
  date.setUTCFullYear(date.getUTCFullYear() - years);
  return date.toISOString().slice(0, 10);
}

/** Mirrors the server's HeroRequestValidator (US-001, SuperHero table). */
export const heroSchema = schema<HeroFormModel>((hero) => {
  const m = HERO_MESSAGES;
  const r = HERO_RULES;

  required(hero.heroName, { message: m.required });
  minLength(hero.heroName, r.heroNameMin, { message: m.heroNameLength });
  maxLength(hero.heroName, r.heroNameMax, { message: m.heroNameLength });

  for (const name of [hero.firstName, hero.lastName]) {
    required(name, { message: m.required });
    maxLength(name, r.personNameMax, { message: m.nameLength });
  }

  required(hero.birthDate, { message: m.required });
  validate(hero.birthDate, ({ value }) => {
    const birthDate = value();
    if (!birthDate) {
      return undefined;
    }

    const today = todayIso();
    if (birthDate >= today) {
      return { kind: 'birthDate.past', message: m.birthDatePast };
    }

    // ISO dates compare correctly as strings.
    return birthDate < yearsBefore(today, r.maxAgeYears)
      ? { kind: 'birthDate.tooOld', message: m.birthDateTooOld }
      : undefined;
  });

  required(hero.email, { message: m.required });
  email(hero.email, { message: m.email });
  maxLength(hero.email, r.emailMax, { message: m.emailLength });

  // One message under the chips for the whole list (count, duplicates and item length), like the server.
  validate(hero.superpowers, ({ value }) => superpowerErrors(value()));

  required(hero.powerLevel, { message: m.powerLevel });
  min(hero.powerLevel, r.powerMin, { message: m.powerLevel });
  max(hero.powerLevel, r.powerMax, { message: m.powerLevel });

  required(hero.address1, { message: m.required });
  maxLength(hero.address1, r.addressMax, { message: m.addressLength });
  maxLength(hero.address2, r.addressMax, { message: m.addressLength });
  required(hero.city, { message: m.required });
  maxLength(hero.city, r.cityMax, { message: m.cityLength });

  required(hero.state, { message: m.state });
  validate(hero.state, ({ value }) =>
    value() && !STATE_CODES.has(value()) ? { kind: 'state.invalid', message: m.state } : undefined,
  );

  required(hero.zipCode, { message: m.required });
  // [0-9] rather than \d, which would accept non-ASCII digits (same rule as the server).
  pattern(hero.zipCode, /^[0-9]{5}(-[0-9]{4})?$/, { message: m.zipCode });
});

function superpowerErrors(superpowers: readonly string[]): ValidationError | undefined {
  const m = HERO_MESSAGES;
  const r = HERO_RULES;
  const trimmed = superpowers.map((p) => p.trim());

  if (trimmed.length < r.superpowersMin || trimmed.length > r.superpowersMax) {
    return { kind: 'superpowers.count', message: m.superpowersCount };
  }

  if (trimmed.some((p) => p.length < r.superpowerMin || p.length > r.superpowerMax)) {
    return { kind: 'superpowers.itemLength', message: m.superpowerLength };
  }

  const distinct = new Set(trimmed.map((p) => p.toLowerCase()));
  return distinct.size < trimmed.length
    ? { kind: 'superpowers.duplicate', message: m.superpowersDuplicate }
    : undefined;
}

export function emptyHero(): HeroFormModel {
  return {
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
  };
}

/** The form model for a loaded hero, or `undefined` while there is none (create mode, loading). */
export function toFormModel(hero: Hero | undefined): HeroFormModel | undefined {
  if (!hero) {
    return undefined;
  }

  return {
    heroName: hero.heroName,
    firstName: hero.firstName,
    lastName: hero.lastName,
    birthDate: hero.birthDate,
    email: hero.email,
    superpowers: [...hero.superpowers],
    powerLevel: hero.powerLevel,
    address1: hero.address1,
    address2: hero.address2 ?? '',
    city: hero.city,
    state: hero.state,
    zipCode: hero.zipCode,
  };
}

/** The request body: trimmed texts, upper-case state, and null for an empty address2. */
export function toHeroRequest(model: HeroFormModel): HeroRequest {
  const address2 = model.address2.trim();
  return {
    heroName: model.heroName.trim(),
    firstName: model.firstName.trim(),
    lastName: model.lastName.trim(),
    birthDate: model.birthDate,
    email: model.email.trim(),
    superpowers: model.superpowers.map((p) => p.trim()),
    powerLevel: model.powerLevel,
    address1: model.address1.trim(),
    address2: address2 || null,
    city: model.city.trim(),
    state: model.state.trim().toUpperCase(),
    zipCode: model.zipCode.trim(),
  };
}
