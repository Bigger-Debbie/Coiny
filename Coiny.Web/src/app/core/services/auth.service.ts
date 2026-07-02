import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";

import { environemnt } from "../../../environments/environment";

import { LoginRequest } from "../../shared/models/auth/login-request";
import { LoginResponse } from "../../shared/models/auth/login-response";

@Injectable({
    providedIn: 'root'
})
export class AuthService {

    private readonly http = inject(HttpClient);
    private readonly apiUrl = `${environemnt.apiUrl}/auth`;

    login(request: LoginRequest): Observable<LoginResponse> {
        return this.http.post<LoginResponse>(
            `${this.apiUrl}/login`,
            request);
    }
}