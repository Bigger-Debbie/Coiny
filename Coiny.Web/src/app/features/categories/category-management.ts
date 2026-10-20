import { Component, computed, DestroyRef, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CategoryService } from '../../core/services/category.service';
import { ToastService, notificationSignal } from '../../core/services/toast.service';
import { Category } from '../../shared/models/category/category';
import { CreateCategory } from '../accounts/create-category/create-category';

@Component({
  selector: 'app-category-management',
  imports: [FormsModule, CreateCategory],
  templateUrl: './category-management.html',
  styleUrl: './category-management.css',
})
export class CategoryManagement {
  private readonly api = inject(CategoryService);
  private readonly toast = inject(ToastService);
  private readonly destroy = inject(DestroyRef);
  private readonly creation = viewChild(CreateCategory);
  readonly modalBusy = computed(() => this.busy() || (this.creation()?.saving() ?? false));
  readonly categories = signal<Category[]>([]);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = notificationSignal('error');
  readonly type = signal('Expense');
  readonly adding = signal(false);
  readonly editing = signal<Category | null>(null);
  showArchived = false;
  name = '';
  constructor() { this.load(); }
  get visible() { return this.categories().filter(c => c.categoryType === this.type() && (this.showArchived || c.isActive)).sort((a,b) => a.name.localeCompare(b.name)); }
  load() {
    this.loading.set(true); this.error.set('');
    this.api.getCategories().pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: rows => { this.categories.set(rows); this.loading.set(false); },
      error: () => { this.loading.set(false); this.error.set('Categories couldn’t load. Please try again.'); },
    });
  }
  edit(category: Category) { this.name = category.name; this.editing.set(category); this.error.set(''); }
  save() { const category = this.editing(); if (category && this.name.trim() && this.name.trim().length <= 100) this.update(category, this.name.trim(), category.isActive); }
  archive(category: Category) {
    if (category.isActive && !window.confirm(`Archive “${category.name}”? Existing transactions will keep this category.`)) return;
    this.update(category, category.name, !category.isActive);
  }
  private update(category: Category, name: string, isActive: boolean) {
    if (this.busy()) return;
    this.busy.set(true); this.error.set('');
    this.api.updateCategory(category.id, { name, isActive }).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: saved => {
        this.categories.update(rows => rows.map(c => c.id === saved.id ? saved : c));
        this.editing.set(null); this.busy.set(false);
        this.toast.success(category.isActive !== saved.isActive ? (saved.isActive ? 'Category restored.' : 'Category archived.') : 'Category updated.');
      },
      error: response => { this.busy.set(false); this.error.set(typeof response.error === 'string' ? response.error : 'Your category couldn’t save. Please try again.'); },
    });
  }
  created(category: Category) {
    this.categories.update(rows => [...rows, category]); this.type.set(category.categoryType); this.adding.set(false);
    this.api.changed.next(); this.toast.success('Category created.');
  }
}
