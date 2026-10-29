import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationStart, NavigationEnd, NavigationCancel, NavigationError, NavigationSkipped, Router } from '@angular/router';
import { PageLoadingService } from '../../core/services/page-loading.service';
import { RouterOutlet } from '@angular/router';

import { AppHeader } from '../../shared/components/app-header/app-header';
import { AppFooter } from '../../shared/components/app-footer/app-footer';

@Component({
  selector: 'app-app-layout',
  imports: [RouterOutlet, AppHeader, AppFooter],
  templateUrl: './app-layout.html',
  styleUrl: './app-layout.css',
})
export class AppLayout {
  readonly loading = inject(PageLoadingService);
  constructor() {
    const destroy = inject(DestroyRef);
    this.loading.begin();
    inject(Router).events.pipe(takeUntilDestroyed(destroy)).subscribe(event => {
      if (event instanceof NavigationStart) this.loading.begin(true);
      else if (event instanceof NavigationEnd) this.loading.ready();
      else if (event instanceof NavigationCancel || event instanceof NavigationError || event instanceof NavigationSkipped) this.loading.cancel();
    });
    destroy.onDestroy(() => this.loading.cancel());
  }
}
