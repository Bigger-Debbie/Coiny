import { ToastService, notificationSignal } from '../../core/services/toast.service';
import { Component, DestroyRef, inject, OnInit, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { UserPreferencesService } from '../../core/services/user-preferences.service';
@Component({
  selector: 'app-user-settings', imports: [FormsModule],
  template: `@if (loading()) { <p role="status">Loading your settings…</p> }
    @else if (loaded()) {
      <form (ngSubmit)="save()"><fieldset [disabled]="busy()">
        <h3>Net worth</h3>
        <label class="preference"><span>Include mortgage in net worth</span><input class="preference-toggle" type="checkbox" role="switch" name="includeMortgage" [(ngModel)]="includeMortgage" aria-describedby="mortgage-preference-help" /></label>
        <p id="mortgage-preference-help">When off, your headline and chart show net worth excluding mortgage. The mortgage balance stays visible separately.</p>
        <p class="hint">This preference is saved for you. Other household members keep their own settings.</p>
        <div class="actions"><button class="button-outline" type="button" (click)="cancelled.emit()">Cancel</button><button class="button-primary" type="submit">{{ busy() ? 'Saving…' : 'Save settings' }}</button></div>
      </fieldset></form>
    }
    @if (error()) { <p role="alert">{{ error() }}</p>@if (!loaded()) { <button class="button-outline" (click)="load()">Try again</button> } }`,
  styles: `fieldset { border:0; padding:0; margin:0; } h3 { font-size:1.2rem; font-weight:400; text-transform:none; letter-spacing:normal; }
    .preference { display:flex; align-items:center; justify-content:space-between; gap:1rem; } p { font-size:.85rem; line-height:1.6; } .hint { color:var(--color-text-muted); }
    .actions { display:flex; justify-content:flex-end; gap:.75rem; margin-top:1.5rem; } [role=alert] { color:var(--color-danger); }
    .preference-toggle { appearance:none; flex-shrink:0; width:2.75rem; height:1.5rem; padding:2px; border:0; border-radius:2rem; background:var(--color-border); cursor:pointer; }
    .preference-toggle::before { content:''; display:block; width:1.25rem; height:1.25rem; border-radius:50%; background:white; }
    .preference-toggle:checked { background:var(--color-primary); } .preference-toggle:checked::before { transform:translateX(1.25rem); }
    .preference-toggle:focus-visible { outline:2px solid var(--color-accent); outline-offset:3px; }`,
})
export class UserSettings implements OnInit {
  private readonly toasts = inject(ToastService);
  private readonly api = inject(UserPreferencesService);
  private readonly destroy = inject(DestroyRef);
  readonly saved = output<void>(); readonly cancelled = output<void>();
  readonly busy = signal(false); readonly loading = signal(true); readonly loaded = signal(false); readonly error = notificationSignal('error');
  includeMortgage = true;
  ngOnInit() { this.load(); }
  load() {
    this.loading.set(true); this.error.set('');
    this.api.get().pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: result => { this.includeMortgage = result.includeMortgageInNetWorth; this.loaded.set(true); this.loading.set(false); }, error: () => { this.error.set('Your settings couldn’t load.'); this.loading.set(false); } });
  }
  save() {
    if (this.busy()) return; this.busy.set(true); this.error.set('');
    this.api.save({ includeMortgageInNetWorth: this.includeMortgage }).pipe(takeUntilDestroyed(this.destroy)).subscribe({ next: () => { this.toasts.success('Settings saved.'); this.busy.set(false); this.saved.emit(); }, error: () => { this.busy.set(false); this.error.set('Your settings couldn’t save. Please try again.'); } });
  }
}
