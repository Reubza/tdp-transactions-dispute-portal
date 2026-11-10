import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { By } from '@angular/platform-browser';
import { Transaction } from '../../../models/transaction.model';
import { DisputeDialogComponent } from './dispute-dialog.component';

const transaction: Transaction = {
  id: 'tx-1',
  accountId: 'acc-1',
  accountName: 'Everyday Cheque',
  maskedAccountNumber: '****5678',
  amount: 1249,
  currency: 'ZAR',
  direction: 'Debit',
  status: 'Posted',
  merchant: 'Takealot.com',
  reference: 'TXN123',
  description: null,
  postedAt: '2026-10-28T10:00:00Z',
  canDispute: true,
  notDisputableReason: null,
  latestDisputeId: null,
  latestDisputeStatus: null,
};

describe('DisputeDialogComponent', () => {
  let fixture: ComponentFixture<DisputeDialogComponent>;
  let http: HttpTestingController;
  const dialogRef = { close: vi.fn(), disableClose: false };

  beforeEach(async () => {
    dialogRef.close.mockReset();
    await TestBed.configureTestingModule({
      imports: [DisputeDialogComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MAT_DIALOG_DATA, useValue: transaction },
        { provide: MatDialogRef, useValue: dialogRef },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(DisputeDialogComponent);
    http = TestBed.inject(HttpTestingController);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  type Form = DisputeDialogComponent['form'];
  const form = () => (fixture.componentInstance as unknown as { form: Form }).form;
  const submit = async () => {
    fixture.debugElement.query(By.css('form')).triggerEventHandler('ngSubmit');
    await fixture.whenStable();
  };

  it('shows the transaction being disputed', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Takealot.com');
    expect(text).toContain('TXN123');
  });

  it('does not submit an incomplete form', async () => {
    form().setValue({ reason: '', description: 'short' });

    await submit();

    http.expectNone('/api/v1/disputes');
    expect(form().controls.reason.hasError('required')).toBe(true);
    expect(form().controls.description.hasError('minlength')).toBe(true);
  });

  it('submits a valid dispute with an idempotency key and closes with the result', async () => {
    form().setValue({ reason: 'Duplicate', description: '  Charged twice for one order  ' });

    await submit();

    const req = http.expectOne('/api/v1/disputes');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      transactionId: 'tx-1',
      reason: 'Duplicate',
      description: 'Charged twice for one order',
    });
    expect(req.request.headers.get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/);
    const created = { summary: { id: 'd-1' } };
    req.flush(created);

    expect(dialogRef.close).toHaveBeenCalledWith(created);
  });

  it('shows the API error and stays open when submission fails', async () => {
    form().setValue({ reason: 'Unauthorised', description: 'I did not make this payment' });

    await submit();
    http
      .expectOne('/api/v1/disputes')
      .flush(
        { title: 'Conflict.', detail: 'This transaction already has an open dispute.' },
        { status: 409, statusText: 'Conflict' },
      );
    await fixture.whenStable();

    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[role=alert]')?.textContent,
    ).toContain('already has an open dispute');
  });
});
