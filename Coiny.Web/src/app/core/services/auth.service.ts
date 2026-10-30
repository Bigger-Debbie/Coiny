import { ToastService } from "./toast.service";
import { DestroyRef, Injectable, inject, signal } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { defer, finalize, Observable, shareReplay, Subject, takeUntil, tap } from "rxjs";
import { jwtDecode } from "jwt-decode";

import { environemnt } from "../../../environments/environment";

import { LoginRequest } from "../../shared/models/auth/login-request";
import { LoginResponse } from "../../shared/models/auth/login-response";
import { CurrentUser } from '../../shared/models/auth/current-user';

interface JwtPayload {
    exp?: number;
}

@Injectable({
    providedIn: 'root'
})
export class AuthService {

    private readonly http = inject(HttpClient);
    private readonly toast = inject(ToastService);
    private readonly apiUrl = `${environemnt.apiUrl}/auth`;
    private readonly tokenKey = 'coiny_token';
    private readonly profile = signal<CurrentUser | null>(null);
    private readonly loadingProfile = signal(false);
    private readonly failedProfile = signal(false);
    private readonly profileInvalidated = new Subject<void>();
    private profileRequest?: Observable<CurrentUser>;

    constructor() {
        const onStorage = (event: StorageEvent) => {
            if (event.storageArea === localStorage && (event.key === this.tokenKey || event.key === null)) {
                this.clearProfile(); window.location.reload();
            }
        };
        window.addEventListener('storage', onStorage);
        inject(DestroyRef).onDestroy(() => window.removeEventListener('storage', onStorage));
    }

    readonly currentUser = this.profile.asReadonly();
    readonly profileLoading = this.loadingProfile.asReadonly();
    readonly profileError = this.failedProfile.asReadonly();

    register(request: { firstName: string; email: string; password: string }): Observable<LoginResponse> {
        return this.http.post<LoginResponse>(`${this.apiUrl}/register`, request);
    }

    login(request: LoginRequest): Observable<LoginResponse> {
        return this.http.post<LoginResponse>(
            `${this.apiUrl}/login`,
            request);
    }

    getCurrentUser(): Observable<CurrentUser> {
        if (!this.profileRequest) {
            this.profileRequest = defer(() => {
                this.loadingProfile.set(true);
                this.failedProfile.set(false);
                return this.http.get<CurrentUser>(`${this.apiUrl}/me`);
            }).pipe(
                takeUntil(this.profileInvalidated),
                tap({
                    next: user => this.profile.set(user),
                    error: () => {
                        this.profile.set(null);
                        this.failedProfile.set(true);
                        this.profileRequest = undefined;
                    }
                }),
                finalize(() => this.loadingProfile.set(false)),
                shareReplay({ bufferSize: 1, refCount: true })
            );
        }
        return this.profileRequest;
    }

    setToken(token: string): void {
        this.clearProfile();
        localStorage.setItem(this.tokenKey, token);
    }

    renewSession(token: string): void {
        localStorage.setItem(this.tokenKey, token);
    }

    getToken(): string | null {
        return localStorage.getItem(this.tokenKey);
    }

    updateNetWorthPreference(includeMortgageInNetWorth: boolean): void { this.profile.update(user => user ? { ...user, includeMortgageInNetWorth } : null); }

    logout(revoke = false): void {
        const token = this.getToken();
        if (revoke && token) this.http.post(`${this.apiUrl}/logout`, {}, { headers: { Authorization: `Bearer ${token}` } }).subscribe({
            error: error => { if (error.status !== 401) this.toast.error('Signed out of this browser. The server could not revoke the session; it will expire automatically.'); }
        });
        localStorage.removeItem(this.tokenKey);
        this.clearProfile();
    }

    private clearProfile(): void {
        // Cancel a pending profile request so a response from the old session
        // cannot repopulate the user after logout or a different login.
        this.profileInvalidated.next();
        this.profileRequest = undefined;
        this.profile.set(null);
        this.loadingProfile.set(false);
        this.failedProfile.set(false);
    }

    isTokenExpired(token: string): boolean {
        try {
            const decoded = jwtDecode<JwtPayload>(token);

            if (!decoded.exp) {
            return true;
            }

            const nowInSeconds = Math.floor(Date.now() / 1000);

            return nowInSeconds >= decoded.exp;
        } catch (error) {
            console.error('JWT decode failed:', error);
            return true;
        }
    }

    isAuthenticated(): boolean {
        const token = this.getToken();

        if (!token) {
            return false;
        }

        if (this.isTokenExpired(token)) { 
            this.logout();
            return false;
        }

        return true;
    }
}
