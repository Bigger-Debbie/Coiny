import { AlertsService } from '../../../core/services/alerts.service';
import { HouseholdAlerts } from '../../../features/alerts/household-alerts';
import { CategoryManagement } from '../../../features/categories/category-management';
import { ChangeDetectorRef, Component, computed, DestroyRef, ElementRef, HostListener, inject, OnInit, signal, ViewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { UserSettings } from '../../../features/settings/user-settings';
import { Modal } from '../modal/modal';
import { HouseholdSettings } from '../../../features/households/household-settings';
import { Icon } from '../icon/icon';

@Component({
  selector: 'app-header',
  imports: [HouseholdAlerts, Icon, RouterLink, RouterLinkActive, Modal, HouseholdSettings, UserSettings, CategoryManagement],
  templateUrl: './app-header.html',
  styleUrl: './app-header.css',
})
export class AppHeader implements OnInit {
  readonly auth = inject(AuthService);
  readonly alerts = inject(AlertsService);
  readonly alertsOpen = signal(false);
  openAlerts() { this.prepareModal(); this.alertsOpen.set(true); }
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly changeDetector = inject(ChangeDetectorRef);
  readonly mobileOpen = signal(false);
  @ViewChild('mobileToggle') private mobileToggle?: ElementRef<HTMLButtonElement>;
  @ViewChild('navigationRoot') private navigationRoot?: ElementRef<HTMLElement>;
  toggleMobile() { if (this.mobileOpen()) this.closeMobile(); else this.openMobile(); }
  openMobile(event?: Event) {
    event?.preventDefault(); this.menuOpen.set(false); this.mobileOpen.set(true);
    this.changeDetector.detectChanges();
    this.navigationRoot?.nativeElement.querySelector<HTMLElement>('.navigation-link')?.focus();
  }
  closeMobile() {
    if (!this.mobileOpen()) return;
    this.mobileOpen.set(false); this.mobileToggle?.nativeElement.focus();
  }
  onNavigationFocusOut(event: FocusEvent) {
    if (this.mobileOpen() && !this.navigationRoot?.nativeElement.contains(event.relatedTarget as Node | null) && event.relatedTarget !== this.mobileToggle?.nativeElement)
      this.mobileOpen.set(false);
  }
  private prepareModal() {
    this.menuOpen.set(false);
    if (this.mobileOpen()) this.closeMobile(); else this.avatarButton?.nativeElement.focus();
  }
  @HostListener('window:resize') onResize() {
    if (window.innerWidth > 760) {
      const active=document.activeElement;
      this.mobileOpen.set(false);
      if (active===this.mobileToggle?.nativeElement || this.navigationRoot?.nativeElement.querySelector('.mobile-profile')?.contains(active)) this.avatarButton?.nativeElement.focus();
    } else {
      if (this.profileRoot?.nativeElement.contains(document.activeElement)) this.mobileToggle?.nativeElement.focus();
      this.menuOpen.set(false);
    }
  }
  readonly categoriesOpen = signal(false);
  openCategories() { this.prepareModal(); this.categoriesOpen.set(true); }
  readonly settingsOpen = signal(false);
  openSettings() { this.prepareModal(); this.settingsOpen.set(true); }
  readonly householdOpen = signal(false);
  readonly menuOpen = signal(false);
  readonly initial = computed(() => Array.from(this.auth.currentUser()?.firstName?.trim() || '')[0]?.toUpperCase() || '?');

  @ViewChild('profileRoot') private profileRoot?: ElementRef<HTMLElement>;
  @ViewChild('avatarButton') private avatarButton?: ElementRef<HTMLButtonElement>;
  @ViewChild('logoutButton') private logoutButton?: ElementRef<HTMLButtonElement>;

  ngOnInit(): void {
    this.destroyRef.onDestroy(()=>this.alerts.stop());
    this.router.events.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(event => { if (event instanceof NavigationEnd) this.closeMobile(); });
    this.auth.getCurrentUser().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => this.alerts.start(),
      error: () => { /* Keep the menu and logout available when profile loading fails. */ },
    });
  }

  openHousehold(): void {
    this.prepareModal();
    this.householdOpen.set(true);
  }

  toggleMenu(): void {
    if (this.menuOpen()) this.menuOpen.set(false);
    else this.openMenu();
  }

  private openMenu(): void {
    this.menuOpen.set(true);
    this.changeDetector.detectChanges();
    this.profileRoot?.nativeElement.querySelector<HTMLElement>('[role="menuitem"]')?.focus();
  }

  onAvatarKeydown(event: KeyboardEvent): void {
    if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;
    event.preventDefault();
    this.openMenu();
    if (event.key === 'ArrowUp') this.logoutButton?.nativeElement.focus();
  }

  onMenuKeydown(event: KeyboardEvent): void {
    if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) {
      event.preventDefault();
      const items = Array.from(this.profileRoot?.nativeElement.querySelectorAll<HTMLElement>('[role="menuitem"]') ?? []);
      const current = items.indexOf(document.activeElement as HTMLElement);
      const index = event.key === 'Home' ? 0 : event.key === 'End' ? items.length - 1
        : (current + (event.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length;
      items[index]?.focus();
    }
  }

  onFocusOut(event: FocusEvent): void {
    if (!this.profileRoot?.nativeElement.contains(event.relatedTarget as Node | null))
      this.menuOpen.set(false);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.mobileOpen() && !this.navigationRoot?.nativeElement.contains(event.target as Node | null) && !this.mobileToggle?.nativeElement.contains(event.target as Node | null)) this.closeMobile();
    if (!this.profileRoot?.nativeElement.contains(event.target as Node | null))
      this.menuOpen.set(false);
  }

  @HostListener('document:keydown.escape', ['$event'])
  onEscape(event: Event): void {
    if (this.mobileOpen()) { event.preventDefault(); this.closeMobile(); return; }
    if (!this.menuOpen()) return;
    event.preventDefault();
    this.menuOpen.set(false);
    this.avatarButton?.nativeElement.focus();
  }

  async logout(): Promise<void> {
    this.menuOpen.set(false); this.closeMobile();
    if (await this.router.navigate(['/login'], { replaceUrl: true })) { this.alerts.stop(); this.auth.logout(); }
  }
}
