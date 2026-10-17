import { ChangeDetectorRef, Component, computed, DestroyRef, ElementRef, HostListener, inject, OnInit, signal, ViewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { Modal } from '../modal/modal';
import { HouseholdSettings } from '../../../features/households/household-settings';
import { Icon } from '../icon/icon';

@Component({
  selector: 'app-header',
  imports: [Icon, RouterLink, RouterLinkActive, Modal, HouseholdSettings],
  templateUrl: './app-header.html',
  styleUrl: './app-header.css',
})
export class AppHeader implements OnInit {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly changeDetector = inject(ChangeDetectorRef);
  readonly householdOpen = signal(false);
  readonly menuOpen = signal(false);
  readonly initial = computed(() => Array.from(this.auth.currentUser()?.firstName?.trim() || '')[0]?.toUpperCase() || '?');

  @ViewChild('profileRoot') private profileRoot?: ElementRef<HTMLElement>;
  @ViewChild('avatarButton') private avatarButton?: ElementRef<HTMLButtonElement>;
  @ViewChild('logoutButton') private logoutButton?: ElementRef<HTMLButtonElement>;

  ngOnInit(): void {
    this.auth.getCurrentUser().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      error: () => { /* Keep the menu and logout available when profile loading fails. */ },
    });
  }

  openHousehold(): void {
    this.menuOpen.set(false);
    this.avatarButton?.nativeElement.focus();
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
    if (!this.profileRoot?.nativeElement.contains(event.target as Node | null))
      this.menuOpen.set(false);
  }

  @HostListener('document:keydown.escape', ['$event'])
  onEscape(event: Event): void {
    if (!this.menuOpen()) return;
    event.preventDefault();
    this.menuOpen.set(false);
    this.avatarButton?.nativeElement.focus();
  }

  async logout(): Promise<void> {
    this.menuOpen.set(false);
    if (await this.router.navigate(['/login'], { replaceUrl: true })) this.auth.logout();
  }
}
