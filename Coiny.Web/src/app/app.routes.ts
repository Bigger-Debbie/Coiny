import { Routes } from '@angular/router';
import { Login } from './features/auth/login/login';
import { Dashboard } from './features/dashboard/dashboard';
import { CreateTransaction } from './features/transactions/create-transaction/create-transaction';
import { authGuard } from './core/guards/auth-guard';
import { AppLayout } from './layouts/app-layout/app-layout';

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
        {
            path: 'dashboard',
            component: Dashboard
        },
        {
            path: 'transactions/new',
            component: CreateTransaction
        },
        {
            path: '',
            redirectTo: 'dashboard',
            pathMatch: 'full'
        }
        ]
    }
];
