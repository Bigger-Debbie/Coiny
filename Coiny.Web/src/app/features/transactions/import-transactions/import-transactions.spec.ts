import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { By } from '@angular/platform-browser';
import { ImportTransactions } from './import-transactions';
import { ImportReview } from './import-review';
import { Component, Input, output } from '@angular/core';

@Component({ selector: 'app-import-review', template: '<p>Review stub</p>' })
class ReviewStub {
  @Input() bankBalance: unknown; @Input() mappedRows: unknown; @Input() account: unknown; @Input() filename = '';
  readonly busyChange = output<boolean>(); readonly lockedChange = output<boolean>(); readonly completed = output<void>();
}

describe('Import transaction mapping screen', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [ImportTransactions], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ accountId: '2' }) }, queryParamMap: of(convertToParamMap({ accountId: '2' })) } },
    ] });
    TestBed.overrideComponent(ImportTransactions, { remove: { imports: [ImportReview] }, add: { imports: [ReviewStub] } });
    http = TestBed.inject(HttpTestingController);
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
  });
  afterEach(() => http.verify());
  async function render() {
    const f = TestBed.createComponent(ImportTransactions); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/accounts')).flush([
      { id: 2, name: 'Checking', accountType: 'Checking', openingBalanceDate: '2026-10-01', isActive: true },
      { id: 3, name: 'Closed', isActive: false },
    ]);
    f.detectChanges(); await f.whenStable(); return f;
  }
  it('maps a final posted bank balance only after explicit order/meaning confirmation', async () => {
    const f = await render(); const c = f.componentInstance;
    c.source = 'Date,Description,Amount,Balance,Status\n10/16/2026,Pending,-5,995,Pending\n10/15/2026,Coffee,-10,1000,Posted'; c.parse();
    Object.assign(c, { amountColumn: 2, positiveMeans: 'In', dateFormat: 'MM/DD/YYYY', confirmed: true });
    c.showPreview(); expect(c.error()).toContain('Confirm the bank order');
    c.balanceOrder = 'NewestFirst'; c.balanceConfirmed = true; c.showPreview();
    expect(c.bankBalance()).toEqual({ date: '2026-10-15', balance: 1000 });
    expect(c.preview()).toHaveLength(2); expect(c.preview()[0].excluded).toBe(true);
  });
  it('preselects an active account and requires date, sign, and confirmation without writing data', async () => {
    const f = await render(); const c = f.componentInstance;
    expect(c.accountId).toBe(2); expect(c.accounts()).toHaveLength(1);
    c.source = 'When,Text,Value\n17/10/2026,Coffee,-4.50'; c.parse();
    c.dateColumn = 0; c.descriptionColumn = 1; c.amountColumn = 2;
    c.showPreview(); expect(c.error()).toContain('date format');
    c.dateFormat = 'DD/MM/YYYY'; c.showPreview(); expect(c.error()).toContain('positive');
    c.positiveMeans = 'In'; c.showPreview(); expect(c.error()).toContain('Confirm');
    c.confirmed = true; c.showPreview(); f.detectChanges(); await f.whenStable();
    expect(c.preview()[0]).toMatchObject({ date: '2026-10-17', direction: 'Out', amount: 4.5 });
    expect(f.nativeElement.querySelector('app-import-review')).toBeTruthy();
    expect(c.step()).toBe('review');
    expect(f.nativeElement.querySelector('#map-date-format')).toBeNull();
    const rows = c.preview(); c.backToMapping(); f.detectChanges(); await f.whenStable();
    expect(c.step()).toBe('mapping'); expect(c.dateFormat).toBe('DD/MM/YYYY');
    c.showPreview(); expect(c.preview()).toBe(rows);
    expect(TestBed.inject(Router).navigate).toHaveBeenCalledWith([], expect.objectContaining({ queryParams: { step: 'review' } }));
    http.expectNone(r => r.method !== 'GET');
  });
  it('supports arbitrary headers, status exclusions, and multi-account filtering', async () => {
    const f = await render(); const c = f.componentInstance;
    c.source = 'A;B;C;D;E\n17.10.2026;Coffee;-4,50;U;ACC1\n17.10.2026;Lunch;-15,00;C;ACC2'; c.parse();
    Object.assign(c, { dateColumn: 0, descriptionColumn: 1, amountColumn: 2, dateFormat: 'DD.MM.YYYY', positiveMeans: 'In', decimalSeparator: ',', statusColumn: 3, accountColumn: 4, confirmed: true });
    c.showPreview(); expect(c.error()).toContain('multiple accounts');
    c.sourceAccount = 'ACC1'; c.excludedStatuses = ['U']; c.showPreview();
    expect(c.preview()).toHaveLength(1); expect(c.preview()[0].excluded).toBe(true);
    expect(c.excludedCount).toBe(1);
    c.invalidate(); expect(c.preview()).toHaveLength(0); expect(c.confirmed).toBe(false);
  });
  it('keeps malformed files recoverable with a header-line override and guards leaving', async () => {
    const f = await render(); const c = f.componentInstance;
    c.source = 'Bank export\nDate,Description,Amount\n2026-10-17,Coffee,-5'; c.parse();
    expect(c.document()).toBeNull(); expect(c.error()).toContain('columns');
    c.headerLine = 2; c.parse(); expect(c.document()?.rows[0].line).toBe(3);
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    expect(c.canLeave()).toBe(false);
    confirm.mockReturnValue(true); expect(c.canLeave()).toBe(true); confirm.mockRestore();
    const event = new Event('beforeunload', { cancelable: true }); c.beforeUnload(event as BeforeUnloadEvent);
    expect(event.defaultPrevented).toBe(true);
  });
  it('reads the chosen file locally and reports decoding errors without posting content', async () => {
    const f = await render(); const c = f.componentInstance;
    const bytes = new TextEncoder().encode('Date,Description,Amount\n2026-10-17,Coffee,-5');
    const file = { name: 'different-bank.csv', size: bytes.length, arrayBuffer: async () => bytes.buffer } as File;
    await c.chooseFile({ target: { files: [file] } } as unknown as Event);
    expect(c.filename).toBe('different-bank.csv'); expect(c.document()?.rows).toHaveLength(1);
    expect(c.reading()).toBe(false); http.expectNone(r => r.method !== 'GET');
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const prefix = new TextEncoder().encode('Date,Description,Amount\n2026-10-17,Caf');
    const suffix = new TextEncoder().encode(',-5');
    const westernBytes = new Uint8Array([...prefix, 0xe9, ...suffix]);
    const western = { name: 'western.csv', size: westernBytes.length, arrayBuffer: async () => westernBytes.buffer } as File;
    await c.chooseFile({ target: { files: [western] } } as unknown as Event);
    expect(c.error()).toContain('encoding'); expect(c.document()).toBeNull();
    c.encoding = 'windows-1252'; await c.readFile();
    expect(c.document()?.rows[0].values[1]).toBe('Café'); confirm.mockRestore();
  });
  it('rebuilds the review only after a mapping changes', async () => {
    const f = await render(); const c = f.componentInstance;
    c.source = 'Date,Description,Amount\n10/16/26,Coffee,-5'; c.parse();
    Object.assign(c, { amountColumn: 2, dateFormat: 'MM/DD/YY', positiveMeans: 'In', confirmed: true });
    c.showPreview(); const original = c.preview(); expect(original[0].date).toBe('2026-10-16');
    c.backToMapping(); c.invalidate(); expect(c.preview()).toHaveLength(0);
    c.confirmed = true; c.showPreview(); expect(c.preview()).not.toBe(original);
  });
});

describe('Mapping and review navigation', () => {
  it('preserves actual row edits when returning to mapping without changing it', async () => {
    TestBed.configureTestingModule({ imports: [ImportTransactions], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ accountId: '1' }) }, queryParamMap: of(convertToParamMap({ accountId: '1' })) } },
    ] });
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
    const http = TestBed.inject(HttpTestingController);
    const f = TestBed.createComponent(ImportTransactions); f.detectChanges();
    http.expectOne(r => r.url.endsWith('/accounts')).flush([{ id: 1, name: 'Checking', openingBalanceDate: '2026-10-01', isActive: true }]);
    f.detectChanges(); await f.whenStable(); const c = f.componentInstance;
    c.source = 'Date,Description,Amount\n10/16/26,Coffee,-5'; c.filename = 'bank.csv'; c.parse();
    Object.assign(c, { dateFormat: 'MM/DD/YY', amountColumn: 2, positiveMeans: 'In', confirmed: true });
    c.showPreview(); f.detectChanges(); await f.whenStable();
    http.expectOne(r => r.url.endsWith('/categories')).flush([{ id: 1, name: 'Dining', categoryType: 'Expense', isActive: true }]);
    http.expectOne(r => r.url.endsWith('/transaction-imports')).flush([]);
    f.detectChanges(); await f.whenStable();
    const child = f.debugElement.query(By.directive(ImportReview)).componentInstance as ImportReview;
    child.edit(child.rows()[0], 'description', 'Morning coffee'); child.edit(child.rows()[0], 'categoryChoice', 'c:1');
    c.backToMapping(); f.detectChanges(); await f.whenStable();
    expect(f.nativeElement.querySelector('app-import-review').hidden).toBe(true);
    expect(c.filename).toBe('bank.csv'); expect(c.dateFormat).toBe('MM/DD/YY');
    c.showPreview(); f.detectChanges(); await f.whenStable();
    const restored = f.debugElement.query(By.directive(ImportReview)).componentInstance as ImportReview;
    expect(restored).toBe(child); expect(restored.rows()[0].description).toBe('Morning coffee');
    expect(restored.rows()[0].categoryChoice).toBe('c:1');
    http.verify();
  });
});
