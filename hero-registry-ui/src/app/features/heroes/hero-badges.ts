import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** The first superpowers as chips, then "+N" whose tooltip and label name every superpower. */
@Component({
  selector: 'app-superpower-chips',
  template: `
    @for (power of shown(); track power) {
      <span class="chip" [attr.data-chip]="power">{{ power }}</span>
    }
    @if (hidden() > 0) {
      <span
        class="chip"
        data-chip="more"
        [attr.title]="all()"
        [attr.aria-label]="'All superpowers: ' + all()"
        >+{{ hidden() }}</span
      >
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-1);
    }

    .chip {
      padding-inline: var(--space-2);
      border: 1px solid var(--color-border);
      border-radius: 999px;
      background: var(--color-bg);
      font-size: 0.8125rem;
      white-space: nowrap;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SuperpowerChips {
  readonly superpowers = input.required<readonly string[]>();
  readonly visible = input(2);

  protected readonly shown = computed(() => this.superpowers().slice(0, this.visible()));
  protected readonly hidden = computed(() =>
    Math.max(0, this.superpowers().length - this.visible()),
  );
  protected readonly all = computed(() => this.superpowers().join(', '));
}

/** The power level (1 to 100) as a bar with an accessible `meter` role, and the number. */
@Component({
  selector: 'app-power-meter',
  template: `
    <span
      class="meter"
      role="meter"
      aria-valuemin="1"
      aria-valuemax="100"
      [attr.aria-valuenow]="value()"
      [attr.aria-label]="'Power level ' + value()"
    >
      <span class="meter__bar" aria-hidden="true"
        ><span class="meter__fill" [style.inline-size.%]="value()"></span
      ></span>
      <span class="meter__value">{{ value() }}</span>
    </span>
  `,
  styles: `
    .meter {
      display: inline-flex;
      align-items: center;
      gap: var(--space-2);
    }

    .meter__bar {
      inline-size: 4rem;
      block-size: 0.5rem;
      border-radius: 999px;
      background: var(--color-border);
      overflow: hidden;
    }

    .meter__fill {
      display: block;
      block-size: 100%;
      background: var(--color-primary);
    }

    .meter__value {
      font-variant-numeric: tabular-nums;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PowerMeter {
  readonly value = input.required<number>();
}
