import { Component } from '@angular/core';
import { SpinningCoin } from '../app-header/spinning-coin';

@Component({
  selector: 'app-auth-logo', imports: [SpinningCoin],
  template: `
    <img class="wordmark" src="assets/logos/Coiny-Wordmark.svg" alt="Coiny" draggable="false" />
    <span class="coin" appSpinningCoin aria-hidden="true"><img src="assets/logos/Coiny-Wordmark-Coin.svg" alt="" draggable="false" /></span>
  `,
  styles: `
    :host { display: block; position: relative; aspect-ratio: 583.07 / 151.58; }
    .wordmark { display: block; width: 100%; height: auto; }
    .coin { position: absolute; inset-block: 0; left: 19.723531%; width: 24.010839%; perspective: 600px; }
    .coin img { display: block; width: 100%; height: 100%; pointer-events: none; user-select: none; }
  `,
})
export class AuthLogo {}
