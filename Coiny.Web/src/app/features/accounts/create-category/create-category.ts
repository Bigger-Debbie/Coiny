import { notificationSignal } from '../../../core/services/toast.service';
import { Component, DestroyRef, inject, output, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CategoryService } from '../../../core/services/category.service';
import { Category } from '../../../shared/models/category/category';
import { CreateCategoryRequest } from '../../../shared/models/category/create-category-request';
import { SelectMenu, SelectOption } from '../../../shared/components/select-menu/select-menu';

@Component({
  selector: 'app-create-category',
  imports: [FormsModule, SelectMenu],
  templateUrl: './create-category.html',
  styleUrl: './create-category.css',
})
export class CreateCategory {
  private readonly service = inject(CategoryService);
  private readonly destroyRef = inject(DestroyRef);
  readonly saved = output<Category>();
  readonly cancelled = output<void>();
  readonly saving = signal(false);
  readonly error = notificationSignal('error');
  readonly model: CreateCategoryRequest = { name: '', categoryType: 'Expense' };
  readonly types: SelectOption[] = [
    { value: 'Expense', label: 'Expense' }, { value: 'Income', label: 'Income' },
  ];

  save(form: NgForm): void {
    if (form.invalid || !this.model.name.trim() || this.saving()) return;
    this.saving.set(true);
    this.error.set('');
    this.service.createCategory({ ...this.model, name: this.model.name.trim() })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: category => { this.saving.set(false); this.saved.emit(category); },
        error: response => {
          this.saving.set(false);
          this.error.set(typeof response.error === 'string'
            ? response.error : 'Your category couldn’t be saved. Check the details and try again.');
        },
      });
  }
  cancel(): void { if (!this.saving()) this.cancelled.emit(); }
}
