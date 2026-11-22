import { Routes } from '@angular/router';
import { authGuard } from './core/auth/guards';
import { Shell } from './layout/shell/shell';

const APP_NAME = 'SuperHero Registry';

export const routes: Routes = [
  {
    path: 'login',
    title: `Sign in · ${APP_NAME}`,
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'heroes' },
      {
        path: 'heroes',
        title: `Heroes · ${APP_NAME}`,
        loadComponent: () =>
          import('./features/heroes/hero-list/hero-list').then((m) => m.HeroList),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
