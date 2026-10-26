import { CsvMappingsService, SavedCsvMapping } from '../../../core/services/csv-mappings.service';
import { rebindCsvMapping } from './saved-csv-mapping';
import { ToastService, notificationSignal } from '../../../core/services/toast.service';
import { Component, effect, DestroyRef, HostListener, inject, Input, OnInit, output, signal, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AccountService } from '../../../core/services/account.service';
import { Account } from '../../../shared/models/account/account';
import { SelectMenu, SelectOption } from '../../../shared/components/select-menu/select-menu';
import { CSV_MAX_BYTES, CsvDelimiter, CsvDocument, CsvError, parseCsv } from './csv-parser';
import { BankBalanceOrder, bankBalanceReference, CsvDateFormat, CsvMapping, ImportReviewRow, mapCsv, suggestColumns } from './csv-mapping';
import { BankBalanceReference, TransactionImportRequest } from '../../../shared/models/transactions/transaction-import';
import { ImportReview } from './import-review';

@Component({
  selector: 'app-import-transactions', imports: [FormsModule, RouterLink, SelectMenu, ImportReview],
  templateUrl: './import-transactions.html', styleUrl: './import-transactions.css',
})
export class ImportTransactions implements OnInit {
  @Input() sessionMode = false;
  @Input() sessionFile: File | null = null;
  @Input() sessionAccountId = 0;
  readonly sessionBusy = output<boolean>();
  constructor() { effect(() => { this.sessionBusy.emit(this.reading() || this.importBusy() || this.loading() || this.profileBusy()); }); }
  readonly reviewed = output<TransactionImportRequest>();
  readonly reviewInvalidated = output<void>();
  @ViewChild(ImportReview) private review?: ImportReview;
  recheckReview() { this.review?.changed(true); }

  private readonly profilesApi=inject(CsvMappingsService);
  private readonly toasts=inject(ToastService);
  readonly profiles=signal<SavedCsvMapping[]>([]);readonly profilesError=signal(false);readonly profileBusy=signal(false);
  profileId=0;profileName='';private pendingProfile:SavedCsvMapping|null=null;
  get compatibleProfiles(){const a=this.selectedAccount;return a?this.profiles().filter(p=>p.accountType===a.accountType && (p.institutionId===null || p.institutionId===a.institutionId)):[];}
  get profileOptions():SelectOption[]{return [{value:0,label:'Choose a saved mapping'},...this.compatibleProfiles.map(p=>({value:p.id,label:p.name}))];}
  get selectedProfile(){return this.compatibleProfiles.find(p=>p.id===Number(this.profileId));}
  selectProfile(){this.profileName=this.selectedProfile?.name??'';}
  loadProfiles(){this.profilesError.set(false);this.profilesApi.list().pipe(takeUntilDestroyed(this.destroy)).subscribe({next:rows=>this.profiles.set(rows),error:()=>this.profilesError.set(true)});}
  async applyProfile(){
    const p=this.selectedProfile;if(!p || this.importBusy() || this.importLocked() || this.profileBusy())return;
    if(this.preview().length && !window.confirm('Load this saved mapping? The current transaction review will be discarded.'))return;
    this.pendingProfile=p;this.encoding=p.settings.encoding;this.delimiter=p.settings.delimiter;this.headerLine=p.settings.headerLine;this.invalidate();this.error.set('');
    if(this.file)await this.readFile();else{this.document.set(null);this.toasts.success('Mapping selected. Choose a CSV file to apply it.');}
  }
  private restoreProfile(p:SavedCsvMapping){
    const m=rebindCsvMapping(p.settings,this.document()!.headers);
    this.dateColumn=m.dateColumn;this.descriptionColumn=m.descriptionColumn;this.dateFormat=m.dateFormat;this.decimalSeparator=m.decimalSeparator??'.';
    this.statusColumn=m.statusColumn??null;this.classificationColumn=m.classificationColumn??null;this.balanceColumn=m.balanceColumn??null;
    this.merchantColumn=m.merchantColumn??null;this.accountColumn=m.accountColumn??null;this.excludedStatuses=[...(m.excludedStatuses??[])];
    this.amountMode=m.amount.mode;this.debitColumn=m.amount.mode==='split'?m.amount.debitColumn:null;this.creditColumn=m.amount.mode==='split'?m.amount.creditColumn:null;
    this.amountColumn=m.amount.mode!=='split'?m.amount.column:null;this.positiveMeans=m.amount.mode==='signed'?m.amount.positiveMeans:null;
    this.directionColumn=m.amount.mode==='indicator'?m.amount.directionColumn:null;this.indicatorDirections=m.amount.mode==='indicator'?{...m.amount.directions}:Object.create(null);
    this.balanceOrder=p.settings.balanceOrder;this.confirmed=false;this.balanceConfirmed=false;this.sourceAccount=null;
    this.toasts.success('Saved mapping loaded. Confirm the account, dates, money direction, and any bank balance before previewing.');
  }
  saveProfile(update=false){
    if(this.profileBusy() || this.importBusy() || this.importLocked() || this.reading())return;
    try {
      if(!this.selectedAccount || !this.document())throw new CsvError('Choose an account and parse a CSV first.');
      const mapping=this.currentMapping();mapCsv(this.document()!,mapping,this.selectedAccount.openingBalanceDate);
      const name=this.profileName.trim();if(!name || name.length>100)throw new CsvError('Name this mapping using up to 100 characters.');
      const p=update?this.selectedProfile:null;if(update && !p)throw new CsvError('Choose the saved mapping to update.');
      this.profileBusy.set(true);this.error.set('');
      this.profilesApi.save(p?.id??null,{name,accountType:this.selectedAccount.accountType,institutionId:this.selectedAccount.institutionId??null,expectedEditVersion:p?.editVersion??0,
        settings:{headers:[...this.document()!.headers],mapping,encoding:this.encoding,delimiter:this.document()!.delimiter,headerLine:this.headerLine,balanceOrder:this.balanceOrder}}).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:saved=>{
          this.profiles.update(rows=>[...rows.filter(row=>row.id!==saved.id),saved].sort((a,b)=>a.name.localeCompare(b.name)));this.profileId=saved.id;this.profileName=saved.name;this.profileBusy.set(false);this.toasts.success('CSV mapping saved.');
        },error:e=>{this.profileBusy.set(false);this.error.set(e.error?.detail||'The mapping couldn’t save. Reload saved mappings and try again.');}});
    }catch(e){this.error.set(e instanceof Error?e.message:'Check the mapping settings.');}
  }
  deleteProfile(){const p=this.selectedProfile;if(!p || this.profileBusy() || !window.confirm('Delete this saved mapping? Your current columns and transactions will stay unchanged.'))return;this.profileBusy.set(true);this.profilesApi.delete(p).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:()=>{this.profiles.update(rows=>rows.filter(row=>row.id!==p.id));this.profileId=0;this.profileName='';this.profileBusy.set(false);this.toasts.success('Saved mapping deleted.');},error:e=>{this.profileBusy.set(false);this.error.set(e.error?.detail||'The mapping couldn’t delete. Reload saved mappings and try again.');}});}
  private readonly accountsApi = inject(AccountService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  readonly accounts = signal<Account[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal(false);
  readonly reading = signal(false);
  readonly error = notificationSignal('error');
  readonly document = signal<CsvDocument | null>(null);
  readonly preview = signal<ImportReviewRow[]>([]);
  readonly importBusy = signal(false);
  readonly importLocked = signal(false);
  readonly importCompleted = signal(false);
  readonly step = signal<'mapping' | 'review'>('mapping');
  accountId = Number(this.route.snapshot.queryParamMap.get('accountId')) || 0;
  filename = '';
  source = '';
  private file?: File;
  private readVersion = 0;
  encoding = 'utf-8';
  delimiter: 'auto' | CsvDelimiter = 'auto';
  headerLine = 1;
  dateColumn: number | null = null;
  descriptionColumn: number | null = null;
  amountMode: 'split' | 'signed' | 'indicator' = 'signed';
  directionColumn: number | null = null;
  indicatorDirections: Record<string, 'In' | 'Out'> = Object.create(null);
  debitColumn: number | null = null;
  creditColumn: number | null = null;
  amountColumn: number | null = null;
  positiveMeans: 'In' | 'Out' | null = null;
  dateFormat: CsvDateFormat | null = null;
  decimalSeparator: '.' | ',' = '.';
  readonly bankBalance = signal<BankBalanceReference | null>(null);
  balanceColumn: number | null = null;
  balanceOrder: BankBalanceOrder | null = null;
  balanceConfirmed = false;
  readonly balanceOrders: SelectOption[] = [{ value: null, label: 'Choose bank order' }, { value: 'NewestFirst', label: 'Newest first (last transaction at the top)' }, { value: 'OldestFirst', label: 'Oldest first (last transaction at the bottom)' }];
  get supportsBalanceCheck() { return ['Checking', 'Savings'].includes(this.selectedAccount?.accountType ?? ''); }
  statusColumn: number | null = null;
  classificationColumn: number | null = null;
  merchantColumn: number | null = null;
  accountColumn: number | null = null;
  sourceAccount: string | null = null;
  excludedStatuses: string[] = [];
  confirmed = false;
  readonly delimiterOptions: SelectOption[] = [
    { value: 'auto', label: 'Detect separator' }, { value: ',', label: 'Comma (,)' },
    { value: ';', label: 'Semicolon (;)' }, { value: '\t', label: 'Tab' },
  ];
  readonly encodings: SelectOption[] = [{ value: 'utf-8', label: 'UTF-8' }, { value: 'windows-1252', label: 'Windows / Western European' }, { value: 'utf-16le', label: 'UTF-16 little-endian' }];
  readonly formats: SelectOption[] = [{ value: null, label: 'Choose date format' },
    ...['MM/DD/YYYY', 'MM/DD/YY', 'DD/MM/YYYY', 'DD/MM/YY', 'YYYY-MM-DD', 'MM-DD-YYYY', 'MM-DD-YY', 'DD-MM-YYYY', 'DD-MM-YY', 'DD.MM.YYYY', 'DD.MM.YY', 'YYYYMMDD'].map(value => ({ value, label: value }))];
  readonly amountModes: SelectOption[] = [{ value: 'signed', label: 'One signed amount column' }, { value: 'split', label: 'Separate money out / money in columns' }, { value: 'indicator', label: 'Amount plus debit/credit indicator column' }];
  readonly indicatorOptions: SelectOption[] = [{ value: null, label: 'Choose direction' }, { value: 'In', label: 'Money in' }, { value: 'Out', label: 'Money out' }];
  readonly signs: SelectOption[] = [{ value: null, label: 'Choose what positive means' }, { value: 'In', label: 'Positive = money in' }, { value: 'Out', label: 'Positive = money out' }];
  readonly decimals: SelectOption[] = [{ value: '.', label: 'Dot: 1,234.56' }, { value: ',', label: 'Comma: 1.234,56' }];
  get accountOptions(): SelectOption[] { return [{ value: 0, label: 'Choose destination account' }, ...this.accounts().map(a => ({ value: a.id, label: a.name, detail: a.institutionName ?? 'No institution' }))]; }
  get selectedAccount() { return this.accounts().find(a => a.id === this.accountId); }
  get columns(): SelectOption[] { return [{ value: null, label: 'Choose column' }, ...(this.document()?.headers ?? []).map((label, value) => ({ value, label: `${value + 1}. ${label}`, detail: this.document()?.rows[0]?.values[value].slice(0, 80) }))]; }
  get optionalColumns(): SelectOption[] { return [{ value: null, label: 'Not present / ignore' }, ...this.columns.slice(1)]; }
  get statuses(): string[] { return this.distinct(this.statusColumn); }
  get indicatorValues(): string[] { return this.distinct(this.directionColumn); }
  get sourceAccounts(): SelectOption[] { return [{ value: null, label: 'Choose the account in this file' }, ...this.distinct(this.accountColumn).map(value => ({ value, label: value || '(empty)' }))]; }
  get rawLines(): string { return this.source.split(/\r\n|\r|\n/).slice(0, 8).map((line, index) => `${index + 1}: ${line}`).join('\n'); }
  get errorsCount(): number { return this.preview().filter(row => !row.excluded && row.errors.length).length; }
  get excludedCount(): number { return this.preview().filter(row => row.excluded).length; }

  ngOnInit(): void {
    this.destroy.onDestroy(() => this.readVersion++);
    this.loadProfiles();
    if (this.sessionMode) { this.accountId = this.sessionAccountId; this.file = this.sessionFile ?? undefined; this.loadAccounts(); if (this.file) void this.readFile(); return; }
    this.loadAccounts();
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroy)).subscribe(params => {
      const review = params.get('step') === 'review';
      if (!review && (this.importBusy() || this.importLocked())) { void this.navigateStep('review', true); return; }
      this.step.set(review && this.preview().length > 0 ? 'review' : 'mapping');
      if (review && !this.preview().length) void this.navigateStep('mapping', true);
    });
  }
  backToMapping(): void { if (!this.importBusy() && !this.importLocked()) void this.navigateStep('mapping'); }
  private navigateStep(step: 'mapping' | 'review', replaceUrl = false): Promise<boolean> {
    this.step.set(step);
    window.scrollTo({ top: 0, behavior: 'instant' });
    if (this.sessionMode) return Promise.resolve(true);
    return this.router.navigate([], { relativeTo: this.route, queryParams: { step: step === 'review' ? 'review' : null }, queryParamsHandling: 'merge', replaceUrl });
  }
  loadAccounts(): void {
    this.loading.set(true); this.loadError.set(false);
    this.accountsApi.getAccounts().pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: accounts => { this.accounts.set(accounts.filter(a => a.isActive)); this.loading.set(false); },
      error: () => { this.loadError.set(true); this.loading.set(false); },
    });
  }
  async chooseFile(event: Event): Promise<void> {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    if (this.source && !window.confirm('Replace this file? Your mapping preview will be discarded.')) return;
    this.file = file;
    await this.readFile();
  }
  async readFile(): Promise<void> {
    if (!this.file || this.importBusy() || this.importLocked()) return;
    const version = ++this.readVersion;
    this.reading.set(true); this.error.set(''); this.preview.set([]); this.document.set(null); this.source = '';
    try {
      if (this.file.size > CSV_MAX_BYTES) throw new CsvError('Choose a CSV file of 2 MB or less.');
      const buffer = await this.file.arrayBuffer();
      if (version !== this.readVersion) return;
      this.filename = this.file.name;
      this.source = new TextDecoder(this.encoding, { fatal: true }).decode(buffer);
      this.parse();
      const profile=this.pendingProfile;this.pendingProfile=null;
      if(profile && this.document()){if(!this.compatibleProfiles.some(p=>p.id===profile.id))throw new CsvError('The selected mapping is not compatible with this account. Choose another mapping or match columns manually.');this.restoreProfile(profile);}
    } catch (error) {
      if (version === this.readVersion) this.error.set(error instanceof CsvError ? error.message : 'This file couldn’t be read. Try another text encoding or export it as CSV again.');
    } finally { if (version === this.readVersion) this.reading.set(false); }
  }
  parse(): void {
    this.invalidate(); this.document.set(null); this.error.set('');
    try {
      const document = parseCsv(this.source, this.delimiter === 'auto' ? undefined : this.delimiter, this.headerLine);
      this.document.set(document);
      const suggested = suggestColumns(document.headers);
      this.balanceColumn = suggested.balanceColumn ?? null; this.balanceOrder = null; this.balanceConfirmed = false;
      this.dateColumn = suggested.dateColumn ?? null; this.descriptionColumn = suggested.descriptionColumn ?? null;
      this.statusColumn = suggested.statusColumn ?? null; this.classificationColumn = suggested.classificationColumn ?? null;
      this.merchantColumn = suggested.merchantColumn ?? null; this.accountColumn = suggested.accountColumn ?? null;
      this.amountMode = suggested.amount?.mode ?? 'signed';
      this.debitColumn = suggested.amount?.mode === 'split' ? suggested.amount.debitColumn : null;
      this.creditColumn = suggested.amount?.mode === 'split' ? suggested.amount.creditColumn : null;
      this.amountColumn = null; this.positiveMeans = null; this.dateFormat = null; this.sourceAccount = null;
      this.directionColumn = null; this.indicatorDirections = Object.create(null);
      this.statusChanged();
    } catch (error) { this.error.set(error instanceof Error ? error.message : 'The CSV couldn’t be parsed.'); }
  }
  invalidate(): void { this.reviewInvalidated.emit(); if (this.importBusy() || this.importLocked()) return; this.preview.set([]); this.bankBalance.set(null); this.confirmed = false; }
  statusChanged(): void { this.excludedStatuses = this.statuses.filter(value => /\bpending\b/i.test(value)); this.invalidate(); }
  toggleStatus(status: string, excluded: boolean): void {
    this.excludedStatuses = excluded ? [...this.excludedStatuses, status] : this.excludedStatuses.filter(value => value !== status);
    this.invalidate();
  }
  private currentMapping():CsvMapping {
    if(!this.dateFormat)throw new CsvError('Choose a date format before saving the mapping.');
    if(this.amountMode==='signed' && !this.positiveMeans)throw new CsvError('Choose what positive amounts mean before saving.');
    return {
        dateColumn: this.dateColumn!, descriptionColumn: this.descriptionColumn!, dateFormat: this.dateFormat,
        amount: this.amountMode === 'split' ? { mode: 'split', debitColumn: this.debitColumn!, creditColumn: this.creditColumn! } :
          this.amountMode === 'indicator' ? { mode: 'indicator', column: this.amountColumn!, directionColumn: this.directionColumn!, directions: this.indicatorDirections } :
            { mode: 'signed', column: this.amountColumn!, positiveMeans: this.positiveMeans! },
        decimalSeparator: this.decimalSeparator, excludedStatuses: this.excludedStatuses,
        statusColumn: this.statusColumn ?? undefined, classificationColumn: this.classificationColumn ?? undefined,
        balanceColumn: this.supportsBalanceCheck ? this.balanceColumn ?? undefined : undefined,
        merchantColumn: this.merchantColumn ?? undefined, accountColumn: this.accountColumn ?? undefined,
      };
  }
  showPreview(): void {
    if (this.importBusy() || this.importLocked() || this.profileBusy()) return;
    if (this.preview().length) { void this.navigateStep('review'); return; }
    this.error.set('');
    try {
      const account = this.selectedAccount;
      if (!account) throw new CsvError('Choose the Coiny account these transactions belong to.');
      if (!this.document()) throw new CsvError('Choose and parse a CSV file first.');
      if (!this.dateFormat) throw new CsvError('Choose the date format used in the file.');
      if (this.amountMode === 'signed' && !this.positiveMeans) throw new CsvError('Confirm whether positive amounts mean money in or money out.');
      if (!this.confirmed) throw new CsvError('Confirm the account, date format, and money direction before previewing.');
      const mapping = this.currentMapping();
      let rows = mapCsv(this.document()!, mapping, account.openingBalanceDate);
      if (new Set(rows.map(row => row.sourceAccount)).size > 1 && this.accountColumn !== null) {
        if (this.sourceAccount === null) throw new CsvError('This file contains multiple accounts. Choose which source account belongs to the selected Coiny account.');
        rows = rows.filter(row => row.sourceAccount === (this.sourceAccount || null));
      }
      if (!rows.length) throw new CsvError('No rows match the chosen source account.');
      if (this.supportsBalanceCheck && this.balanceColumn !== null) {
        if (!this.balanceOrder || !this.balanceConfirmed) throw new CsvError('Confirm the bank order and posted running-balance meaning, or ignore the balance column.');
        this.bankBalance.set(bankBalanceReference(rows, this.balanceOrder));
      } else this.bankBalance.set(null);
      this.preview.set(rows);
      void this.navigateStep('review');
    } catch (error) { this.error.set(error instanceof Error ? error.message : 'Check the column mappings.'); }
  }
  restart(): void { this.bankBalance.set(null); this.balanceConfirmed = false; this.preview.set([]); this.document.set(null); this.source = ''; this.file = undefined;this.pendingProfile=null; this.filename = ''; this.importLocked.set(false); this.importCompleted.set(false); this.error.set(''); this.confirmed = false; void this.navigateStep('mapping', true); }
  canLeave(): boolean {
    return !this.reading() && !this.importBusy() && !this.profileBusy() && (this.importCompleted() || !this.source || window.confirm(this.importLocked()
      ? 'The import result is not confirmed. Retry this batch before leaving to avoid starting it again. Leave anyway?'
      : 'Leave transaction import? Your file and unsaved review will be discarded.'));
  }
  @HostListener('window:beforeunload', ['$event']) beforeUnload(event: BeforeUnloadEvent): void {
    if (!this.importCompleted() && (this.source || this.reading() || this.importBusy() || this.profileBusy())) { event.preventDefault(); event.returnValue = ''; }
  }
  private distinct(column: number | null): string[] { return column === null ? [] : [...new Set(this.document()?.rows.map(row => row.values[column]?.trim() ?? '') ?? [])].sort(); }
}
