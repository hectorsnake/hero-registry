import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { hero } from '../../../../testing/heroes';
import { ToastService } from '../../../core/ui/toast-service';
import { HERO_MESSAGES, US_STATES } from '../hero-schema';
import { HeroForm } from './hero-form';

describe('HeroForm', () => {
  let backend: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-22T12:00:00Z'));
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    backend = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  afterEach(() => {
    vi.useRealTimers();
    backend.verify();
  });

  async function create(id?: string) {
    const fixture = TestBed.createComponent(HeroForm);
    if (id !== undefined) {
      fixture.componentRef.setInput('id', id);
    }
    TestBed.tick();
    return page(fixture);
  }

  async function createMode() {
    const v = await create();
    await v.fixture.whenStable();
    return v;
  }

  async function editMode() {
    const v = await create('7');
    backend.expectOne('/api/heroes/7').flush(hero());
    await v.fixture.whenStable();
    return v;
  }

  function page(fixture: ComponentFixture<HeroForm>) {
    const element = fixture.nativeElement as HTMLElement;
    const field = <T extends HTMLElement = HTMLInputElement>(label: string) => {
      const id = Array.from(element.querySelectorAll('label')).find(
        (l) => l.textContent?.trim() === label,
      )?.htmlFor;
      return element.querySelector<T>(`#${id}`)!;
    };
    const errorFor = (label: string) => {
      const id = field(label).getAttribute('aria-describedby')?.split(' ')[0];
      return element.querySelector(`#${id}`)?.textContent?.trim() ?? '';
    };
    const button = (name: string) =>
      Array.from(element.querySelectorAll('button')).find((b) => b.textContent?.trim() === name);
    const v = {
      fixture,
      element,
      field,
      errorFor,
      button,
      text: () => element.textContent ?? '',
      async type(label: string, value: string) {
        const input = field(label);
        input.value = value;
        input.dispatchEvent(new Event('input'));
        await fixture.whenStable();
      },
      async select(label: string, value: string) {
        const select = field<HTMLSelectElement>(label);
        select.value = value;
        // A browser fires both on a select; [formField] listens to `input`.
        select.dispatchEvent(new Event('input'));
        select.dispatchEvent(new Event('change'));
        await fixture.whenStable();
      },
      async addSuperpower(name: string) {
        const input = field('Superpowers');
        input.value = name;
        input.dispatchEvent(new Event('input'));
        input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', cancelable: true }));
        await fixture.whenStable();
      },
      async submit() {
        element.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
        await fixture.whenStable();
      },
      async fillValidHero() {
        await v.type('Hero name', '  Spider-Man ');
        await v.type('First name', 'Peter');
        await v.type('Last name', 'Parker');
        await v.type('Birth date', '2001-08-10');
        await v.type('Email', 'peter@dailybugle.com');
        await v.addSuperpower(' Agility ');
        await v.type('Power level', '85');
        await v.type('Address line 1', '20 Ingram Street');
        await v.type('City', 'New York');
        await v.select('State', 'NY');
        await v.type('ZIP code', '11375');
      },
    };
    return v;
  }

  describe('create mode', () => {
    it('starts empty, titled for a new hero, without loading anything', async () => {
      const v = await createMode();

      expect(v.element.querySelector('h1')?.textContent?.trim()).toBe('Register a hero');
      expect(v.field('Hero name').value).toBe('');
      expect(v.element.querySelectorAll('[data-chip]')).toHaveLength(0);
      backend.expectNone('/api/heroes/7');
    });

    it('submits the trimmed DTO with the superpowers array, then shows a toast and the new hero', async () => {
      const v = await createMode();
      await v.fillValidHero();

      await v.submit();
      const req = backend.expectOne('/api/heroes');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        heroName: 'Spider-Man',
        firstName: 'Peter',
        lastName: 'Parker',
        birthDate: '2001-08-10',
        email: 'peter@dailybugle.com',
        superpowers: ['Agility'],
        powerLevel: 85,
        address1: '20 Ingram Street',
        address2: null,
        city: 'New York',
        state: 'NY',
        zipCode: '11375',
      });
      req.flush(hero({ id: 9, heroName: 'Spider-Man' }), { status: 201, statusText: 'Created' });

      await vi.waitFor(() => expect(router.navigate).toHaveBeenCalledWith(['/heroes', 9]));
      expect(
        TestBed.inject(ToastService)
          .toasts()
          .map((t) => t.text),
      ).toEqual(['Hero saved.']);
    });

    it('an invalid submit sends nothing, shows the errors and focuses the first invalid field', async () => {
      const v = await createMode();

      await v.submit();

      backend.expectNone('/api/heroes');
      expect(v.errorFor('Hero name')).toBe(HERO_MESSAGES.required);
      expect(v.errorFor('Superpowers')).toBe(HERO_MESSAGES.superpowersCount);
      expect(document.activeElement).toBe(v.field('Hero name'));
    });

    it('a 409 puts the server message on heroName and focuses it', async () => {
      const v = await createMode();
      await v.fillValidHero();

      await v.submit();
      backend.expectOne('/api/heroes').flush(
        {
          title: 'Conflict',
          status: 409,
          code: 'hero.duplicate',
          errors: { heroName: ['This hero name is already registered.'] },
        },
        { status: 409, statusText: 'Conflict' },
      );
      await vi.waitFor(() =>
        expect(v.errorFor('Hero name')).toBe('This hero name is already registered.'),
      );

      expect(document.activeElement).toBe(v.field('Hero name'));
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('a 400 maps every returned field, including superpowers[0]', async () => {
      const v = await createMode();
      await v.fillValidHero();

      await v.submit();
      backend.expectOne('/api/heroes').flush(
        {
          title: 'Invalid',
          status: 400,
          errors: {
            email: ['Email is not valid.'],
            'superpowers[0]': ['Too short.'],
            zipCode: ['Bad ZIP.'],
          },
        },
        { status: 400, statusText: 'Bad Request' },
      );

      await vi.waitFor(() => expect(v.errorFor('Email')).toBe('Email is not valid.'));
      expect(v.errorFor('Superpowers')).toBe('Too short.');
      expect(v.errorFor('ZIP code')).toBe('Bad ZIP.');
    });

    it('disables Save while the request is pending', async () => {
      const v = await createMode();
      await v.fillValidHero();

      await v.submit();

      expect(v.button('Saving…')?.disabled).toBe(true);
      backend.expectOne('/api/heroes').flush(hero({ id: 9 }));
    });

    it('Cancel goes back to the list', async () => {
      const v = await createMode();

      v.button('Cancel')!.click();

      expect(router.navigate).toHaveBeenCalledWith(['/heroes'], expect.anything());
    });
  });

  describe('edit mode', () => {
    it('fills the form from the loaded hero (linkedSignal), titled with its name', async () => {
      const v = await editMode();

      expect(v.element.querySelector('h1')?.textContent?.trim()).toBe('Edit Superman');
      expect(v.field('Hero name').value).toBe('Superman');
      expect(v.field<HTMLSelectElement>('State').value).toBe('NY');
      expect(v.element.querySelectorAll('[data-chip]')).toHaveLength(4);
    });

    it('shows a loading state until the hero arrives', async () => {
      const v = await create('7');

      expect(v.text()).toContain('Loading');
      backend.expectOne('/api/heroes/7').flush(hero());
    });

    it('saves with PUT /api/heroes/{id}', async () => {
      const v = await editMode();
      await v.type('Hero name', 'Superman Prime');

      await v.submit();
      const req = backend.expectOne('/api/heroes/7');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body.heroName).toBe('Superman Prime');
      req.flush(hero({ heroName: 'Superman Prime' }));

      await vi.waitFor(() => expect(router.navigate).toHaveBeenCalledWith(['/heroes', 7]));
    });

    it('Cancel goes back to the hero', async () => {
      const v = await editMode();

      v.button('Cancel')!.click();

      expect(router.navigate).toHaveBeenCalledWith(['/heroes', 7]);
    });

    it('a hero that does not exist shows "Hero not found"', async () => {
      const v = await create('99');
      backend
        .expectOne('/api/heroes/99')
        .flush(
          { title: 'Not Found', status: 404, code: 'hero.not_found' },
          { status: 404, statusText: 'Not Found' },
        );
      await v.fixture.whenStable();

      expect(v.text()).toContain('Hero not found');
      expect(v.element.querySelector('form')).toBeNull();
    });
  });

  describe('unsaved changes', () => {
    it('there are none before the user edits anything', async () => {
      const v = await editMode();

      expect(v.fixture.componentInstance.hasUnsavedChanges()).toBe(false);
    });

    it('editing a field leaves unsaved changes', async () => {
      const v = await createMode();

      await v.type('Hero name', 'Storm');

      expect(v.fixture.componentInstance.hasUnsavedChanges()).toBe(true);
    });

    it('there are none once the hero is saved', async () => {
      const v = await createMode();
      await v.fillValidHero();

      await v.submit();
      backend.expectOne('/api/heroes').flush(hero({ id: 9 }));
      await vi.waitFor(() => expect(router.navigate).toHaveBeenCalled());

      expect(v.fixture.componentInstance.hasUnsavedChanges()).toBe(false);
    });
  });

  describe('controls', () => {
    it('the birth date is a native date picker limited to today', async () => {
      const v = await createMode();

      expect(v.field('Birth date').type).toBe('date');
      expect(v.field('Birth date').max).toBe('2026-11-22');
    });

    it('the power level has a slider and a number bound to the same field', async () => {
      const v = await createMode();
      const slider = v.element.querySelector<HTMLInputElement>('input[type="range"]')!;

      await v.type('Power level', '72');

      expect(slider.value).toBe('72');
      expect(slider.getAttribute('aria-label')).toBe('Power level slider');
    });

    it('the state is a select of every accepted code, and the ZIP code opens a numeric keypad', async () => {
      const v = await createMode();

      const options = v
        .field<HTMLSelectElement>('State')
        .querySelectorAll('option:not([value=""])');
      expect(options).toHaveLength(US_STATES.length);
      expect(v.field('ZIP code').getAttribute('inputmode')).toBe('numeric');
    });
  });
});
