import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";

import { environemnt } from "../../../environments/environment";
import { DashboardSummary } from "../../shared/models/dashboard/dashboard-summary";

@Injectable({
    providedIn: 'root'
})
export class DashboardService { 
    private readonly http = inject(HttpClient);
    private readonly apiUrl = `${environemnt.apiUrl}/dashboard`;

    getSummary(): Observable<DashboardSummary> {
        return this.http.get<DashboardSummary>(this.apiUrl);
    }
}