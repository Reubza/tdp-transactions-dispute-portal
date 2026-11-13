# Resolve: architecture and design notes

How the system is built and why. To build, run and test it, see the [README](../README.md).

- [Features](#features)
- [Business rules](#business-rules)
- [Architecture](#architecture)
- [API](#api)
- [Test coverage](#test-coverage)
- [Configuration](#configuration)
- [Production-readiness notes](#production-readiness-notes)
- [Assumptions and trade-offs](#assumptions-and-trade-offs)
- [Possible next steps](#possible-next-steps)

## Features

**Customer**

- Transactions list across accounts — server-side paging, search (merchant / reference / description), account and
  date-range filters, and a "disputed / not disputed" filter. Amounts are signed, account numbers are masked.
- Each transaction shows whether it can be disputed and, if not, why (e.g. "already has an open dispute",
  "only posted transactions can be disputed", "outside the 120-day window").
- Raise a dispute with a reason and description. Submission is **idempotent** (each dialog sends an
  `Idempotency-Key`), so a retried request can never create two disputes.
- **My disputes** — the historic view: every dispute ever raised, filterable by open/closed, status and text.
- Dispute detail with a **status timeline** (who did what, when, and any notes) and the final outcome.
- Withdraw an open dispute.

**Agent (back office)**

- Dispute queue across all customers (defaults to open disputes), with search by customer, merchant or reference.
- Start review, uphold or decline (a resolution note is required to resolve). Every change appears in the customer's
  timeline.

## Business rules

| Rule                                                                                 | Where enforced                                                        |
| ------------------------------------------------------------------------------------ | --------------------------------------------------------------------- |
| Customers only ever see their own accounts, transactions and disputes (others → 404) | Query filters in the application services                             |
| Only **posted debit** transactions can be disputed                                   | `Transaction.CheckDisputable` (domain)                                |
| Disputes must be raised within **120 days** of posting (configurable)                | Domain + `Disputes:DisputeWindowDays`                                  |
| At most **one open dispute** per transaction (a new one is allowed after closure)    | Application check **and** a filtered unique index in PostgreSQL        |
| Lifecycle `Submitted → UnderReview → Upheld \| Rejected`; `Withdrawn` while open      | `Dispute` aggregate state machine                                     |
| Every status change is recorded, append-only                                         | `DisputeStatusChange` rows written by the aggregate                   |
| Concurrent updates to the same dispute don't silently overwrite each other           | Optimistic concurrency on PostgreSQL `xmin` → `409 Conflict`          |

## Architecture

```
┌────────────────────────── Docker image (non-root, :8080) ───────────────────────────┐
│  Angular SPA (static files in wwwroot)                                              │
│        │  HTTPS/JSON, Bearer JWT                                                    │
│        ▼                                                                            │
│  Api              Controllers, auth policies, ProblemDetails,                       │
│                   rate limiting, health checks, OpenAPI, security headers           │
│        ▼                                                                            │
│  Application      Use-case services, DTOs, FluentValidation                         │
│        ▼                                                                            │
│  Domain           Entities + rules (Dispute state machine, window)                  │
│        ▲                                                                            │
│  Infrastructure   EF Core/Npgsql, migrations, JWT issuing,                          │
│                   PBKDF2 password hashing, demo seeding                             │
└────────────────────────────────────────┬────────────────────────────────────────────┘
                                         ▼
                                   PostgreSQL 17
```

Clean architecture: dependencies point inwards; the domain has no framework dependencies. The application layer
depends on an `IApplicationDbContext` abstraction (pragmatically exposing EF `DbSet`s rather than a repository per
aggregate) and on `ICurrentUser`, `ITokenIssuer` and `IPasswordHasher`.

```
src/
  Domain/            Users, Accounts, Transactions, Disputes (+ status history)
  Application/       Auth, Transactions, Disputes (customer + agent) services
  Infrastructure/    Persistence (DbContext, configurations, migrations, seeder), Auth (JWT, hashing)
  Api/               Program.cs, Controllers/, Common/ (middleware, auth policies, error handling)
web/src/app/         Angular app: components/, services/, guards/, interceptors/, models/
tests/
  UnitTests/         Domain rules + application services (EF in-memory)
  IntegrationTests/  Real HTTP against the real app + real PostgreSQL (Testcontainers)
```

## API

All endpoints are under `/api/v1` and return [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) problem details on
errors (with a stable `code` for business-rule failures, e.g. `dispute.already_open`, `dispute.window_expired`).

| Method | Path                                 | Role     | Purpose                                         |
| ------ | ------------------------------------ | -------- | ----------------------------------------------- |
| POST   | `/auth/login`                        | –        | E-mail + password → JWT (rate-limited)          |
| GET    | `/accounts`                          | Customer | The customer's accounts                         |
| GET    | `/transactions`                      | Customer | Paged, filtered transactions                    |
| GET    | `/transactions/{id}`                 | Customer | One transaction                                 |
| POST   | `/disputes`                          | Customer | Raise a dispute (`Idempotency-Key` supported)   |
| GET    | `/disputes`                          | Customer | Dispute history (paged, filterable)             |
| GET    | `/disputes/{id}`                     | Customer | Dispute with timeline                           |
| POST   | `/disputes/{id}/withdraw`            | Customer | Withdraw an open dispute                        |
| GET    | `/agent/disputes`                    | Agent    | Queue across all customers                      |
| GET    | `/agent/disputes/{id}`               | Agent    | Dispute with timeline                           |
| POST   | `/agent/disputes/{id}/status`        | Agent    | `StartReview` / `Uphold` / `Reject` (+ note)    |
| GET    | `/health/live`, `/health/ready`      | –        | Liveness / readiness (database)                 |

## Test coverage

- **Domain:** dispute eligibility (direction, status, window boundary), every lifecycle transition (valid and invalid),
  history recording, withdrawal ownership, resolution notes.
- **Application:** ownership scoping, duplicate-dispute prevention, re-dispute after closure, idempotent create and key
  reuse, validation, history filters, agent workflow reflected in customer history, transaction dispute flags/search.
- **Startup:** a missing connection string and an unreachable database fail fast with an actionable message that
  never includes the password.
- **Integration (HTTP + PostgreSQL):** login, authentication and role authorisation (401/403), cross-customer isolation
  (404), paging/validation problems, raise → duplicate (409), idempotent replay (201 then 200), window expiry (422),
  withdraw twice (422), agent resolution visible in the customer timeline, security headers and correlation IDs,
  migrations and seed data.
- **Frontend:** auth session handling, route guard, auth interceptor, dispute form validation/submission/error states.

## Configuration

Standard ASP.NET Core configuration: `appsettings.json` → `appsettings.{Environment}.json` → environment variables
(`Section__Key`) → command line.

| Key                                  | Default            | Notes                                                              |
| ------------------------------------ | ------------------ | ------------------------------------------------------------------ |
| `ConnectionStrings:DisputePortal`    | –                  | **Required.** Npgsql connection string                             |
| `Jwt:SigningKey`                     | –                  | **Required**, ≥ 32 chars. Validated at startup                     |
| `Jwt:Issuer` / `Jwt:Audience`        | `dispute-portal…`  |                                                                    |
| `Jwt:AccessTokenLifetimeMinutes`     | `30`               | The SPA signs the user out when the token expires                  |
| `Disputes:DisputeWindowDays`         | `120`              |                                                                    |
| `Database:MigrateOnStartup`          | `false`            | `true` in Development and docker-compose                           |
| `Database:StartupConnectTimeoutSeconds` | `30`          | How long startup waits for PostgreSQL before failing with a clear message |
| `Database:Seed:Enabled`              | `false`            | Seeds demo data into an empty database only                        |
| `Database:Seed:DemoPassword`         | –                  | Required when seeding                                              |
| `Api:EnableDocs`                     | Dev only           | Serves `/openapi/v1.json` and `/scalar`                            |
| `RateLimiting:LoginAttemptsPerMinute`| `10`               | Login attempts allowed per client IP                               |
| `Serilog:*`                          | JSON to stdout     | Structured logs with a correlation id per request                  |

## Production-readiness notes

- **Security:** PBKDF2 password hashing (ASP.NET Core Identity hasher) with constant-time behaviour for unknown users;
  short-lived HS256 JWTs with issuer/audience/lifetime validation; deny-by-default authorisation (fallback policy) plus
  role policies; login rate limiting; resource ownership enforced in queries (returns 404 rather than 403 to avoid
  enumeration); strict CSP, `X-Frame-Options`, `nosniff`, `Referrer-Policy`, `no-store` on API responses; HSTS outside
  Development; no secrets in source (dev defaults only); open-redirect-safe login return URLs; non-root container.
- **Reliability:** database constraints back up every invariant the application checks; optimistic concurrency;
  idempotent create; EF Core connection resiliency; liveness/readiness probes; graceful problem responses for all
  failure modes, with a `traceId` that matches the logs.
- **Operability:** structured JSON logging (Serilog) with correlation IDs (`X-Correlation-ID` honoured/propagated),
  health checks, config validated at startup (fail fast), forwarded headers for running behind a load balancer.
- **Maintainability:** clean architecture, analyzers with warnings-as-errors, nullable reference types, `.editorconfig`,
  Prettier, CI on every change.

## Assumptions and trade-offs

- Transactions are owned by the core banking ledger; the portal treats them as read-only reference data (seeded here).
- Authentication is a self-contained JWT login to keep the solution runnable anywhere. In production this would be
  delegated to the bank's identity provider (OIDC with PKCE, refresh tokens, MFA) and the API would only validate tokens.
- Tokens are kept in `sessionStorage` (cleared when the tab closes). With an IdP, a BFF with HttpOnly cookies would be
  preferred to keep tokens out of JavaScript entirely.
- Currency formatting assumes a single currency per account (ZAR in the demo data).

## Possible next steps

- Integrate with the card scheme / chargeback system and publish dispute events via a transactional outbox
  (notifications to customers, SLA tracking).
- Evidence uploads (receipts) with virus scanning; provisional credit handling.
- Agent assignment, SLAs and four-eyes approval for high-value disputes; fuller audit (who viewed what).
- End-to-end browser tests (Playwright), load tests, OpenTelemetry tracing/metrics, ESLint for the SPA.
