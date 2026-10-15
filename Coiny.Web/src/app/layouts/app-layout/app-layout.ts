import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { AppHeader } from '../../shared/components/app-header/app-header';
import { AppFooter } from '../../shared/components/app-footer/app-footer';

@Component({
  selector: 'app-app-layout',
  imports: [RouterOutlet, AppHeader, AppFooter],
  templateUrl: './app-layout.html',
  styleUrl: './app-layout.css',
})
export class AppLayout {}
