import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Placeholder: the heroes list, detail and form are built in T14. */
@Component({
  selector: 'app-hero-list',
  template: `
    <h1>Heroes</h1>
    <p>The heroes list is added in T14.</p>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeroList {}
