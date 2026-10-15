import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AppLayout } from './app-layout';
describe('AppLayout', () => {
  it('renders the shared header and footer around routed content', () => {
    TestBed.configureTestingModule({ imports: [AppLayout], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    const fixture = TestBed.createComponent(AppLayout);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(r => r.url.endsWith('/auth/me')).flush({ id: 'alex', email: 'alex@example.test', firstName: 'Alex' });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-header')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('app-footer')).toBeTruthy();
    http.verify();
  });
});
