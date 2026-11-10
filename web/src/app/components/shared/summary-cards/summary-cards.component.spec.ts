import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Account } from '../../../models/transaction.model';
import { SummaryCardsComponent } from './summary-cards.component';

const accounts: Account[] = [
  {
    id: 'a1',
    name: 'Everyday Cheque',
    maskedAccountNumber: '****5678',
    type: 'Cheque',
    currency: 'ZAR',
  },
  {
    id: 'a2',
    name: 'Platinum Credit Card',
    maskedAccountNumber: '****9876',
    type: 'CreditCard',
    currency: 'ZAR',
  },
];

const page = (totalCount: number) => ({
  items: [],
  page: 1,
  pageSize: 1,
  totalCount,
  totalPages: 1,
});

describe('SummaryCardsComponent', () => {
  let fixture: ComponentFixture<SummaryCardsComponent>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SummaryCardsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    fixture = TestBed.createComponent(SummaryCardsComponent);
    fixture.componentRef.setInput('accounts', accounts);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  it('shows each account with its masked number', async () => {
    http.match(() => true).forEach((req) => req.flush(page(0)));
    await fixture.whenStable();

    expect(text()).toContain('Everyday Cheque');
    expect(text()).toContain('****5678');
    expect(text()).toContain('Credit card');
  });

  it('shows open and closed dispute counts from the API', async () => {
    http.expectOne((r) => r.params.get('open') === 'true').flush(page(2));
    http.expectOne((r) => r.params.get('open') === 'false').flush(page(3));
    await fixture.whenStable();

    const counts = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.count'),
    ).map((el) => el.textContent?.trim());
    expect(counts).toEqual(['2', '3']);
  });

  it('shows a placeholder when counts cannot be loaded', async () => {
    http
      .match(() => true)
      .forEach((req) => req.flush(null, { status: 500, statusText: 'Server Error' }));
    await fixture.whenStable();

    const counts = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.count'),
    ).map((el) => el.textContent?.trim());
    expect(counts).toEqual(['–', '–']);
  });
});
