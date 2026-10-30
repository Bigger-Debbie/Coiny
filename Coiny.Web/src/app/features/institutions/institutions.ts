import { Component, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import { Institution, InstitutionService } from '../../core/services/institution.service';
import { AccountService } from '../../core/services/account.service';
import { Account } from '../../shared/models/account/account';
import { ToastService } from '../../core/services/toast.service';

@Component({ selector: 'app-institutions', imports: [FormsModule], templateUrl: './institutions.html', styleUrl: './institutions.css' })
export class Institutions {
  private readonly api = inject(InstitutionService);
  private readonly accountsApi = inject(AccountService);
  private readonly toast = inject(ToastService);
  private readonly destroy = inject(DestroyRef);
  readonly institutions = signal<Institution[]>([]);
  readonly accounts = signal<Account[]>([]);
  readonly expanded = signal<ReadonlySet<number>>(new Set());
  toggleAccounts(id: number) { this.expanded.update(current => { const next = new Set(current); if (next.has(id)) next.delete(id); else next.add(id); return next; }); }
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly editing = signal<Institution | null>(null);
  readonly stale = signal(false);
  name = ''; website = '';
  constructor() { this.load(); }
  accountsFor(id: number) { return this.accounts().filter(a => a.institutionId === id).sort((a,b) => a.name.localeCompare(b.name)); }
  accountType(type: string) { return type.replace(/([a-z])([A-Z])/g, '$1 $2'); }
  websiteLink(website: string | null) { return website && /^https?:\/\//i.test(website) ? website : null; }
  get websiteValid() { if (!this.website.trim()) return true; try { const url = new URL(this.website.trim()); return ['https:', 'http:'].includes(url.protocol) && !!url.hostname && !url.username && !url.password; } catch { return false; } }
  get dirty() { const original = this.editing(); return !!original && (this.name.trim() !== original.name || (this.website.trim() || null) !== original.website); }
  load() {
    if (this.busy() || (this.dirty && !window.confirm('Reload institutions and discard your unsaved changes?'))) return;
    this.loading.set(true); this.error.set(''); this.editing.set(null); this.stale.set(false);
    forkJoin({ institutions: this.api.getInstitutions(), accounts: this.accountsApi.getAccounts() }).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: data => { this.institutions.set(data.institutions); this.accounts.set(data.accounts); this.loading.set(false); },
      error: () => { this.loading.set(false); this.error.set('Institutions couldn’t load. Please try again.'); },
    });
  }
  edit(institution: Institution) { if (this.busy() || this.editing()) return; this.editing.set(institution); this.name = institution.name; this.website = institution.website ?? ''; this.error.set(''); this.stale.set(false); }
  cancel() { if (!this.busy()) { this.editing.set(null); this.error.set(''); this.stale.set(false); } }
  save(form: NgForm) {
    const institution = this.editing();
    if (!institution || this.busy() || this.stale() || !this.dirty) return;
    if (form.invalid || !this.name.trim() || this.name.trim().length > 100 || this.website.trim().length > 250 || !this.websiteValid) return;
    this.busy.set(true); this.error.set('');
    this.api.updateInstitution(institution, { name: this.name.trim(), website: this.website.trim() || null }).pipe(takeUntilDestroyed(this.destroy)).subscribe({
      next: saved => { this.institutions.update(rows => rows.map(i => i.id === saved.id ? saved : i).sort((a,b) => a.name.localeCompare(b.name))); this.editing.set(null); this.busy.set(false); this.toast.success('Institution updated.'); },
      error: response => { this.busy.set(false); this.stale.set(response.status === 409 || response.status === 404); this.error.set(response.error?.detail ?? 'The institution couldn’t be saved. Please try again.'); },
    });
  }
}
