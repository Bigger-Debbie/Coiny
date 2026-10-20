import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable, Subject, tap } from "rxjs";

import { environemnt } from "../../../environments/environment";
import { Category } from "../../shared/models/category/category";
import { CreateCategoryRequest } from "../../shared/models/category/create-category-request";

@Injectable({
    providedIn: 'root'
})
export class CategoryService {
    readonly changed = new Subject<void>();
    private readonly http = inject(HttpClient);
    private readonly apiUrl = `${environemnt.apiUrl}/categories`;

    getCategories(): Observable<Category[]> {
        return this.http.get<Category[]>(this.apiUrl);
    }
    updateCategory(id: number, request: { name: string; isActive: boolean }): Observable<Category> {
        return this.http.put<Category>(`${this.apiUrl}/${id}`, request).pipe(tap(() => this.changed.next()));
    }
    createCategory(request: CreateCategoryRequest): Observable<Category> {
        return this.http.post<Category>(this.apiUrl, request);
    }
}
