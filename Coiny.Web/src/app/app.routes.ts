import { Routes } from '@angular/router';
import { Login } from './features/auth/login/login';
import { Dashboard } from './features/dashboard/dashboard';
import { authGuard } from './core/guards/auth-guard';
import { AppLayout } from './layouts/app-layout/app-layout';
import { Accounts } from './features/accounts/accounts';

export const routes: Routes = [
    {
        path: 'login',
        component: Login
    },
    {
        path: '',
        component: AppLayout,
        canActivate: [authGuard],
        children: [
        { path: 'accounts/new', redirectTo: ({ queryParams }) => '/accounts?' + new URLSearchParams({ ...queryParams, create: 'account' }).toString() },
        { path: 'accounts', component: Accounts },
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
