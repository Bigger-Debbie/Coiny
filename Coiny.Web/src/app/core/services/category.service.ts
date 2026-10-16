import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";

import { environemnt } from "../../../environments/environment";
import { Category } from "../../shared/models/category/category";
import { CreateCategoryRequest } from "../../shared/models/category/create-category-request";

@Injectable({
    providedIn: 'root'
})
export class CategoryService { 
    private readonly http = inject(HttpClient);
    private readonly apiUrl = `${environemnt.apiUrl}/categories`;

    getCategories(): Observable<Category[]> {
        return this.http.get<Category[]>(this.apiUrl);
    }
    createCategory(request: CreateCategoryRequest): Observable<Category> {
        return this.http.post<Category>(this.apiUrl, request);
    }
}
