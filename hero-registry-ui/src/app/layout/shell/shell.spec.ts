import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AuthService } from '../../core/auth/auth-service';
import { APP_NAME, Shell } from './shell';

@Component({ template: '<p>page</p>' })
class Page {}

describe('Shell', () => {
  it('shows the app name when routed with component input binding (as in app.config)', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          [{ path: '', component: Shell, children: [{ path: 'heroes', component: Page }] }],
          withComponentInputBinding(),
        ),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            currentUser: signal(null),
            isAuthenticated: signal(false),
            isAdmin: signal(false),
          },
        },
      ],
    });
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/heroes');

    expect(harness.routeNativeElement?.textContent).toContain(APP_NAME);
    expect(harness.routeNativeElement?.textContent).toContain('page');
  });
});
