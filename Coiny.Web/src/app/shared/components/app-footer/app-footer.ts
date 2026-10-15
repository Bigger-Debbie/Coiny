import { Component } from '@angular/core';

@Component({
  selector: 'app-footer',
  templateUrl: './app-footer.html',
  styleUrl: './app-footer.css',
})
export class AppFooter {
  readonly year = new Date().getFullYear();
}
