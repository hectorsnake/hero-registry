import { TestBed } from '@angular/core/testing';
import { MOBILE_QUERY, ViewportService } from './viewport-service';

/** A controllable MediaQueryList, because jsdom does not implement matchMedia. */
class FakeMediaQueryList {
  readonly listeners = new Set<(event: { matches: boolean }) => void>();

  constructor(
    readonly media: string,
    public matches: boolean,
  ) {}

  addEventListener(_type: 'change', listener: (event: { matches: boolean }) => void): void {
    this.listeners.add(listener);
  }

  removeEventListener(_type: 'change', listener: (event: { matches: boolean }) => void): void {
    this.listeners.delete(listener);
  }

  change(matches: boolean): void {
    this.matches = matches;
    this.listeners.forEach((listener) => listener({ matches }));
  }
}

describe('ViewportService', () => {
  let query: FakeMediaQueryList;

  function mockMatchMedia(matches: boolean): void {
    vi.stubGlobal(
      'matchMedia',
      (media: string) => (query = new FakeMediaQueryList(media, matches)),
    );
  }

  afterEach(() => vi.unstubAllGlobals());

  it('watches the mobile breakpoint', () => {
    mockMatchMedia(false);

    TestBed.inject(ViewportService);

    expect(query.media).toBe(MOBILE_QUERY);
    expect(MOBILE_QUERY).toBe('(max-width: 768px)');
  });

  it('isMobile starts from the current viewport', () => {
    mockMatchMedia(true);

    expect(TestBed.inject(ViewportService).isMobile()).toBe(true);
  });

  it('isMobile follows changes of the media query', () => {
    mockMatchMedia(false);
    const viewport = TestBed.inject(ViewportService);

    query.change(true);
    expect(viewport.isMobile()).toBe(true);

    query.change(false);
    expect(viewport.isMobile()).toBe(false);
  });

  it('removes its listener when the application is destroyed', () => {
    mockMatchMedia(false);
    TestBed.inject(ViewportService);
    expect(query.listeners.size).toBe(1);

    TestBed.resetTestingModule();

    expect(query.listeners.size).toBe(0);
  });

  it('treats an environment without matchMedia as a desktop', () => {
    vi.stubGlobal('matchMedia', undefined);

    expect(TestBed.inject(ViewportService).isMobile()).toBe(false);
  });
});
