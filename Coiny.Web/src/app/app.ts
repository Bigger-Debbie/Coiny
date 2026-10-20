import { Component, signal } from '@angular/core';
import { Toasts } from './shared/components/toast/toast';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Toasts],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly title = signal('Coiny.Web');
}
