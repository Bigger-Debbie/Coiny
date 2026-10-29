import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { defer, finalize } from 'rxjs';
import { environemnt } from '../../../environments/environment';
import { PageLoadingService } from '../services/page-loading.service';

export const pageLoadingInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith(environemnt.apiUrl + '/')) return next(request);
  const loading = inject(PageLoadingService);
  return defer(() => {
    const tracked = loading.requestStarted();
    return next(request).pipe(finalize(() => loading.requestFinished(tracked)));
  });
};
