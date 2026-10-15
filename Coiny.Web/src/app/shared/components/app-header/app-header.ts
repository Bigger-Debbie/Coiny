import { ChangeDetectorRef, Component, computed, DestroyRef, ElementRef, HostListener, inject, OnInit, signal, ViewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { Icon } from '../icon/icon';

@Component({
  selector: 'app-header',
  imports: [Icon],
  templateUrl: './app-header.html',
  styleUrl: './app-header.css',
})
export class AppHeader implements OnInit {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly changeDetector = inject(ChangeDetectorRef);
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

  toggleMenu(): void {
    if (this.menuOpen()) this.menuOpen.set(false);
    else this.openMenu();
  }

  private openMenu(): void {
    this.menuOpen.set(true);
    this.changeDetector.detectChanges();
    this.logoutButton?.nativeElement.focus();
  }

  onAvatarKeydown(event: KeyboardEvent): void {
    if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;
    event.preventDefault();
    this.openMenu();
  }

  onMenuKeydown(event: KeyboardEvent): void {
    if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) {
      event.preventDefault();
      this.logoutButton?.nativeElement.focus();
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

  logout(): void {
    this.menuOpen.set(false);
    this.auth.logout();
    void this.router.navigate(['/login'], { replaceUrl: true });
  }
}
