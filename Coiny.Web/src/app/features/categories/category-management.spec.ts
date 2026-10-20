import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CategoryManagement } from './category-management';
import { ToastService } from '../../core/services/toast.service';

const food = { id: 1, name: 'Food', categoryType: 'Expense', isActive: true, isSystem: false };
describe('CategoryManagement', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  function render() {
    const f = TestBed.createComponent(CategoryManagement); const http = TestBed.inject(HttpTestingController);
    http.expectOne(r => r.url.endsWith('/categories')).flush([food, { ...food, id: 2, name: 'Old', isActive: false }, { ...food, id: 3, name: 'Pay', categoryType: 'Income' }]);
    f.detectChanges(); return { f, http, c: f.componentInstance };
  }
  it('filters income/expense and lets users reveal archived categories', () => {
    const { c } = render(); expect(c.visible.map(c => c.name)).toEqual(['Food']);
    c.showArchived = true; expect(c.visible.map(c => c.name)).toEqual(['Food', 'Old']);
    c.type.set('Income'); expect(c.visible.map(c => c.name)).toEqual(['Pay']);
  });
  it('renames and archives without changing type, then restores', () => {
    const { c, http } = render(); c.edit(food); c.name = 'Groceries'; c.save();
    const update = http.expectOne(r => r.method === 'PUT'); expect(update.request.body).toEqual({ name: 'Groceries', isActive: true });
    update.flush({ ...food, name: 'Groceries' }); expect(c.editing()).toBeNull();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    c.archive(c.categories()[0]); http.expectOne(r => r.method === 'PUT').flush({ ...food, name: 'Groceries', isActive: false });
    expect(c.visible).toHaveLength(0);
    c.archive(c.categories()[0]); http.expectOne(r => r.method === 'PUT').flush({ ...food, name: 'Groceries' });
    expect(c.visible).toHaveLength(1); expect(TestBed.inject(ToastService).messages().at(-1)?.message).toBe('Category restored.');
  });
  it('keeps the edit form open on failure and reports the error', () => {
    const { c, http } = render(); c.edit(food); c.save();
    http.expectOne(r => r.method === 'PUT').flush('Name already exists.', { status: 400, statusText: 'Bad Request' });
    expect(c.error()).toBe('Name already exists.'); expect(c.editing()?.id).toBe(1); expect(c.busy()).toBe(false);
  });
});
