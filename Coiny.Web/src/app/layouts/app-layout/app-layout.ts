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
    const router = inject(Router);
    let loadingNavigation: number | undefined;
    let initialLoad = true;
    router.events.pipe(takeUntilDestroyed(destroy)).subscribe(event => {
      if (event instanceof NavigationStart) {
        // Modal refreshes and selection changes can update query parameters or
        // fragments. Only a different routed page should cover the content.
        const pagePath = (url: string) => {
          const tree = router.parseUrl(url);
          tree.queryParams = {};
          tree.fragment = null;
          return router.serializeUrl(tree);
        };
        const currentPage = pagePath(router.url);
        const nextPage = pagePath(event.url);
        if (currentPage !== nextPage) {
          loadingNavigation = event.id;
          this.loading.begin(true);
        }
      } else if (event instanceof NavigationEnd) {
        if (initialLoad || loadingNavigation === event.id) this.loading.ready();
        initialLoad = false;
        if (loadingNavigation === event.id) loadingNavigation = undefined;
      } else if (event instanceof NavigationCancel || event instanceof NavigationError || event instanceof NavigationSkipped) {
        if (initialLoad || loadingNavigation === event.id) {
          this.loading.cancel();
          loadingNavigation = undefined;
          initialLoad = false;
        }
      }
    });
    destroy.onDestroy(() => this.loading.cancel());
  }
}
