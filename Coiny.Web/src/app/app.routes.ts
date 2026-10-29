import type { ImportHistory } from './features/transactions/import-history/import-history';
import type { ImportSession } from './features/transactions/import-session/import-session';
import { Routes } from '@angular/router';
import { Login } from './features/auth/login/login';
import { Dashboard } from './features/dashboard/dashboard';
import { householdGuard } from './core/guards/household-guard';
import { authGuard } from './core/guards/auth-guard';
import { AppLayout } from './layouts/app-layout/app-layout';
import type { SettingsPage } from './features/settings/settings-page';
import type { Budget } from './features/budget/budget';
import { Accounts } from './features/accounts/accounts';

export const routes: Routes = [
    { path: 'recover-with-code', loadComponent: () => import('./features/auth/recovery/recovery').then(m => m.PasswordRecovery) },
    { path: 'forgot-password', loadComponent: () => import('./features/auth/recovery/recovery').then(m => m.PasswordRecovery) },
    { path: 'reset-password', loadComponent: () => import('./features/auth/recovery/recovery').then(m => m.PasswordRecovery) },
    { path: 'signup', loadComponent: () => import('./features/auth/signup/signup').then(m => m.Signup) },
    { path: 'household-setup', canActivate: [authGuard], loadComponent: () => import('./features/households/household-setup').then(m => m.HouseholdSetup) },
    {
        path: 'login',
        component: Login
    },
    {
        path: '',
        component: AppLayout,
        canActivate: [authGuard],
        canActivateChild: [householdGuard],
        children: [
        { path: 'accounts/import-session', redirectTo: ({ queryParams }) => '/accounts/import?' + new URLSearchParams(queryParams).toString() },
        { path: 'accounts/import-history', loadComponent: () => import('./features/transactions/import-history/import-history').then(m => m.ImportHistory), canDeactivate: [(component: ImportHistory) => component.canLeave()] },
        { path: 'accounts/import', loadComponent: () => import('./features/transactions/import-session/import-session').then(m => m.ImportSession), canDeactivate: [(component: ImportSession) => component.canLeave()] },
        { path: 'accounts/new', redirectTo: ({ queryParams }) => '/accounts?' + new URLSearchParams({ ...queryParams, create: 'account' }).toString() },
        { path: 'household', redirectTo: '/settings', pathMatch: 'full' },
        { path: 'settings', loadComponent: () => import('./features/settings/settings-page').then(m => m.SettingsPage), canDeactivate: [(component: SettingsPage) => component.canLeave()] },
        { path: 'accounts', component: Accounts },
        { path: 'budget', loadComponent: () => import('./features/budget/budget').then(m => m.Budget), canDeactivate: [(component: Budget) => component.canLeave()] },
        {
            path: 'dashboard',
            component: Dashboard
        },
        {
            path: 'transactions/new',
            redirectTo: ({ queryParams }) => '/accounts?' + new URLSearchParams({ ...queryParams, create: 'transaction' }).toString()
        },
        {
            path: '',
            redirectTo: 'dashboard',
            pathMatch: 'full'
        }
        ]
    }
];
