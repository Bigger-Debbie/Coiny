import { Component, signal } from '@angular/core';

@Component({
  selector: 'app-coin-background',
  template: `
    <div class="coins" aria-hidden="true" [class.paused]="paused()">
      @for (coin of coins; track $index) {
        <img src="assets/icons/Coiny-Coin-Outline.svg" alt=""
          [style.left.%]="coin.left" [style.width.px]="coin.size"
          [style.animation-duration.s]="coin.duration" [style.animation-delay.s]="coin.delay"
          [style.--rotation]="coin.rotation + 'deg'" />
      }
    </div>
    <button type="button" (click)="paused.set(!paused())" [attr.aria-pressed]="paused()">
      {{ paused() ? 'Resume background animation' : 'Pause background animation' }}
    </button>
  `,
  styleUrl: './coin-background.css',
})
export class CoinBackground {
  readonly paused = signal(false);
  readonly coins = Array.from({ length: 14 }, (_, i) => ({
    left: (i * 37 + 3) % 100, size: 52 + (i % 4) * 13,
    duration: 28 + (i % 5) * 5, delay: -(i * 7 + 3),
    rotation: (i % 2 ? 1 : -1) * (12 + i * 9),
  }));
}
