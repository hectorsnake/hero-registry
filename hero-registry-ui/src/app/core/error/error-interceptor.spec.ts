import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { ToastService } from '../ui/toast-service';
import {
  FORBIDDEN_MESSAGE,
  OFFLINE_MESSAGE,
  SERVER_ERROR_MESSAGE,
  errorInterceptor,
} from './error-interceptor';

describe('errorInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  const toasts = { show: vi.fn() };

  beforeEach(() => {
    toasts.show.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: toasts },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  async function failWith(status: number, body: string | object): Promise<unknown> {
    const result = firstValueFrom(http.get('/api/heroes'));
    backend.expectOne('/api/heroes').flush(body, { status, statusText: 'Error' });
    return result.catch((error: unknown) => error);
  }

  it('a 403 shows "You don\'t have permission to do that." and still fails the request', async () => {
    const error = await failWith(403, { title: 'Forbidden', status: 403, code: 'auth.forbidden' });

    expect(toasts.show).toHaveBeenCalledWith(FORBIDDEN_MESSAGE, 'error');
    expect(FORBIDDEN_MESSAGE).toBe("You don't have permission to do that.");
    expect(error).toMatchObject({ status: 403 });
  });

  it('a 5xx shows the problem title', async () => {
    await failWith(500, { title: 'An unexpected error occurred.', status: 500, traceId: '00-1' });

    expect(toasts.show).toHaveBeenCalledWith('An unexpected error occurred.', 'error');
  });

  it('a 5xx without a problem body never shows the raw server text', async () => {
    await failWith(502, '<html><body>nginx: Bad Gateway, upstream at 10.0.0.5</body></html>');

    expect(toasts.show).toHaveBeenCalledWith(SERVER_ERROR_MESSAGE, 'error');
  });

  it('a network failure (status 0) says the server cannot be reached', async () => {
    const result = firstValueFrom(http.get('/api/heroes'));
    backend.expectOne('/api/heroes').error(new ProgressEvent('error'), { status: 0 });

    await expect(result).rejects.toMatchObject({ status: 0 });
    expect(toasts.show).toHaveBeenCalledWith(OFFLINE_MESSAGE, 'error');
  });

  it.each([400, 409])('a %s passes through to the form without a toast', async (status) => {
    const error = await failWith(status, {
      title: 'Invalid',
      status,
      errors: { heroName: ['Taken.'] },
    });

    expect(error).toMatchObject({ status });
    expect(toasts.show).not.toHaveBeenCalled();
  });

  it.each([401, 404])('a %s is left to its owner without a toast', async (status) => {
    await failWith(status, { title: 'x', status });

    expect(toasts.show).not.toHaveBeenCalled();
  });

  it('successful responses are untouched', async () => {
    const result = firstValueFrom(http.get('/api/heroes'));
    backend.expectOne('/api/heroes').flush(['hero']);

    expect(await result).toEqual(['hero']);
    expect(toasts.show).not.toHaveBeenCalled();
  });
});
