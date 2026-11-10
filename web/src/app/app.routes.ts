import { Routes } from '@angular/router';
import { anonymousGuard, homeRedirectGuard, roleGuard } from './guards/auth.guard';
import { LayoutComponent } from './components/layout/layout.component';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [anonymousGuard],
    title: 'Sign in · Dispute Portal',
    loadComponent: () => import('./components/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: '',
    component: LayoutComponent,
    children: [
      {
        path: 'transactions',
        canActivate: [roleGuard],
        data: { role: 'Customer' },
        title: 'Transactions · Dispute Portal',
        loadComponent: () =>
          import('./components/transactions/transactions.component').then(
            (m) => m.TransactionsComponent,
          ),
      },
      {
        path: 'disputes',
        canActivate: [roleGuard],
        data: { role: 'Customer' },
        title: 'My disputes · Dispute Portal',
        loadComponent: () =>
          import('./components/disputes/disputes.component').then((m) => m.DisputesComponent),
      },
      {
        path: 'disputes/:id',
        canActivate: [roleGuard],
        data: { role: 'Customer', mode: 'customer' },
        title: 'Dispute · Dispute Portal',
        loadComponent: () =>
          import('./components/disputes/dispute-detail/dispute-detail.component').then(
            (m) => m.DisputeDetailComponent,
          ),
      },
      {
        path: 'agent/disputes',
        canActivate: [roleGuard],
        data: { role: 'Agent', mode: 'agent' },
        title: 'Dispute queue · Dispute Portal',
        loadComponent: () =>
          import('./components/disputes/disputes.component').then((m) => m.DisputesComponent),
      },
      {
        path: 'agent/disputes/:id',
        canActivate: [roleGuard],
        data: { role: 'Agent', mode: 'agent' },
        title: 'Dispute · Dispute Portal',
        loadComponent: () =>
          import('./components/disputes/dispute-detail/dispute-detail.component').then(
            (m) => m.DisputeDetailComponent,
          ),
      },
      { path: '', pathMatch: 'full', canActivate: [homeRedirectGuard], children: [] },
    ],
  },
  { path: '**', redirectTo: '' },
];
