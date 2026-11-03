import { Routes } from '@angular/router';
import { anonymousGuard, homeRedirectGuard, roleGuard } from './core/auth.guard';
import { ShellComponent } from './layout/shell.component';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [anonymousGuard],
    title: 'Sign in · Dispute Portal',
    loadComponent: () => import('./features/login/login.page').then((m) => m.LoginPage),
  },
  {
    path: '',
    component: ShellComponent,
    children: [
      {
        path: 'transactions',
        canActivate: [roleGuard],
        data: { role: 'Customer' },
        title: 'Transactions · Dispute Portal',
        loadComponent: () =>
          import('./features/transactions/transactions.page').then((m) => m.TransactionsPage),
      },
      {
        path: 'disputes',
        canActivate: [roleGuard],
        data: { role: 'Customer' },
        title: 'My disputes · Dispute Portal',
        loadComponent: () =>
          import('./features/disputes/disputes.page').then((m) => m.DisputesPage),
      },
      {
        path: 'disputes/:id',
        canActivate: [roleGuard],
        data: { role: 'Customer', mode: 'customer' },
        title: 'Dispute · Dispute Portal',
        loadComponent: () =>
          import('./features/disputes/dispute-detail.page').then((m) => m.DisputeDetailPage),
      },
      {
        path: 'agent/disputes',
        canActivate: [roleGuard],
        data: { role: 'Agent', mode: 'agent' },
        title: 'Dispute queue · Dispute Portal',
        loadComponent: () =>
          import('./features/disputes/disputes.page').then((m) => m.DisputesPage),
      },
      {
        path: 'agent/disputes/:id',
        canActivate: [roleGuard],
        data: { role: 'Agent', mode: 'agent' },
        title: 'Dispute · Dispute Portal',
        loadComponent: () =>
          import('./features/disputes/dispute-detail.page').then((m) => m.DisputeDetailPage),
      },
      { path: '', pathMatch: 'full', canActivate: [homeRedirectGuard], children: [] },
    ],
  },
  { path: '**', redirectTo: '' },
];
