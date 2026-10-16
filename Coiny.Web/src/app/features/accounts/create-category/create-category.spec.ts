import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CreateCategory } from './create-category';

describe('CreateCategory', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [CreateCategory], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  async function render() {
    const f = TestBed.createComponent(CreateCategory); f.detectChanges(); await f.whenStable(); return f;
  }
  async function enterName(f: Awaited<ReturnType<typeof render>>, name: string) {
    const input = f.nativeElement.querySelector('#category-name') as HTMLInputElement;
    input.value = name; input.dispatchEvent(new Event('input')); await f.whenStable();
  }
  async function submit(f: Awaited<ReturnType<typeof render>>) {
    f.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await f.whenStable();
  }
  it('rejects blank names without posting', async () => {
    const f = await render(); await enterName(f, '   '); await submit(f);
    http.expectNone(r => r.method === 'POST');
    expect(f.nativeElement.textContent).toContain('Enter a category name.');
  });
  it('trims names, prevents duplicate submissions, and emits the created category', async () => {
    const f = await render();
    const saved = vi.fn(); f.componentInstance.saved.subscribe(saved);
    await enterName(f, ' Entertainment '); await submit(f); await submit(f);
    const request = http.expectOne(r => r.method === 'POST' && r.url.endsWith('/categories'));
    expect(request.request.body).toEqual({ name: 'Entertainment', categoryType: 'Expense' });
    expect(f.componentInstance.saving()).toBe(true);
    const category = { id: 8, name: 'Entertainment', categoryType: 'Expense', isActive: true, isSystem: false };
    request.flush(category);
    expect(saved).toHaveBeenCalledWith(category);
    expect(f.componentInstance.saving()).toBe(false);
  });
  it('keeps values and shows duplicate-name errors so the user can retry', async () => {
    const f = await render(); await enterName(f, 'Groceries'); await submit(f);
    http.expectOne(r => r.method === 'POST').flush('A category with this name already exists', { status: 400, statusText: 'Bad Request' });
    await f.whenStable();
    expect(f.nativeElement.querySelector('[role="alert"]').textContent).toContain('already exists');
    expect(f.componentInstance.model.name).toBe('Groceries');
    expect(f.componentInstance.saving()).toBe(false);
    await enterName(f, 'Household groceries'); await submit(f);
    http.expectOne(r => r.method === 'POST').flush({ id: 9, name: 'Household groceries', categoryType: 'Expense' });
  });
  it('offers Income and Expense and cancels without a request', async () => {
    const f = await render();
    expect(f.componentInstance.types.map(o => o.value)).toEqual(['Expense', 'Income']);
    const cancelled = vi.fn(); f.componentInstance.cancelled.subscribe(cancelled);
    f.componentInstance.cancel();
    expect(cancelled).toHaveBeenCalledOnce();
    http.expectNone(r => r.method === 'POST');
  });
});
