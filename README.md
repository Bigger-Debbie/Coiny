# Coiny

Coiny is a household finance app for tracking balances, transactions, budgets,
savings funds, and net worth. It uses ASP.NET Core / EF Core 10, ASP.NET Core
Identity, JWT authentication, and Angular 21.

## Current status

The agreed v1 feature scope is implemented and merged into `main`. Local integration,
migration, and recovery rehearsals are complete; public hosting is not deployed.
Release preparation now focuses on production packaging, Azure infrastructure,
monitoring, scheduled offsite backups, and a reviewed live-data cutover.
See the [roadmap](docs/11-roadmap.md) for release gates and deferred refinements.

| Area | Implemented |
| --- | --- |
| Accounts and history | Account/category management, opening balances, daily snapshots, trends, debt/mortgage views, statement reconciliation and later-change review flags |
| Transactions and imports | Manual entries/transfers, editing/deletion, multi-account CSV review, duplicate checks, transfer matching, merchant/category rules, saved mappings, import correction and safe undo |
| Budgeting | Resumable initial setup, published item editing with effective dates, monthly and income-period views, calendar, multiple linked transactions per payment, skipped savings contributions, actuals and forecasts through one year |
| Savings funds | Account-backed funds with component goals, recurring costs/reserves, existing-cash allocations, paycheck schedules, affordability reviews and fund activity |
| Recurring activity | Manual and optional daily detection, reviewable suggestions, household alerts with personal unread counts |
| Account security | Password change/reset, optional SMTP recovery, offline recovery codes, revocable sessions, lockout and rate limits; unified Household/Preferences/Security settings |
| Household data | Owner-only encrypted backups, optional portable backup password, restore preview and safety backup, CSV ZIP export; available on hybrid storage |
| Storage and local deployment | PostgreSQL identity/orchestration plus per-household SQLite finances, guarded migration/maintenance/operator recovery tools, Caddy/Compose review and recovery labs |

Household files provide data separation; Coiny still processes financial data on
the server. Client-held encryption keys and zero-knowledge privacy are not implemented.

## Get started

Choose the setup that matches your task:

- **Normal local development:** [setup guide](docs/06-development-setup.md), then
  optional [demo data](docs/05-demo-data.md). The default remains one combined SQLite
  database, with frontend `http://localhost:4200` and API `http://localhost:5298/api`.
- **Fresh hybrid installation / portable recovery test:**
  [recovery lab](deploy/recovery-lab/README.md) on `http://localhost:6082`.
  Docker runs Caddy/web, API, and PostgreSQL containers with separate persistent volumes.
- **Disposable synthetic integration tests:** [Docker test tooling](deploy/docker-test/README.md).
  Its former persistent lab has been retired; running its commands may recreate it.

The private personal review installation on `http://localhost:6081` has separate
configuration and data outside the repository. The recovery lab has also been used
for a successful user-driven portable restore. Neither installation's volumes are
now disposable test data. These loopback-only labs are not production templates.

Hybrid mode is explicitly enabled by configuration; pulling the code does not
migrate an existing SQLite database. Follow the [storage and delivery guide](docs/21-hybrid-storage-and-azure.md)
and [operator runbook](deploy/operations/README.md). Never run two installations
accepting writes against divergent copies of the same household.

## Repository layout

```text
Coiny.API/       API, models, services, legacy and split-provider EF migrations
Coiny.API.Tests/ Backend regression and real-provider integration tests
Coiny.Web/       Angular app, shared UI, styles, frontend tests
Coiny.slnx      .NET solution
deploy/         Local Compose labs and offline migration/backup tooling
experiments/    Retained synthetic architecture proof and historical evidence
docs/           Product behavior, setup, API, architecture, and operations guides
```

Exact versions are in [Coiny.API.csproj](Coiny.API/Coiny.API.csproj),
[package.json](Coiny.Web/package.json), and the frontend lockfile.

## Documentation

| Guide | Purpose |
| --- | --- |
| [Project vision](docs/00-project-vision.md) | Audience, goals, and product direction |
| [Development principles](docs/01-development-principles.md) | Engineering and collaboration conventions |
| [Domain blueprint](docs/02-domain-blueprint.md) | Entities, ownership, and storage boundaries |
| [Balances, snapshots, and trends](docs/03-account-trends.md) | Financial rules and service responsibilities |
| [Authentication and user profile](docs/04-user-profile.md) | Sign-in, personalization, and household membership |
| [Demo data](docs/05-demo-data.md) | Legacy development seed and reset instructions |
| [Development setup](docs/06-development-setup.md) | Configuration, startup, verification, troubleshooting |
| [API reference](docs/07-api-reference.md) | Endpoints, payloads, and Postman workflow |
| [Dashboard and frontend](docs/08-dashboard.md) | UI behavior, charts, and page/data transitions |
| [Household budgeting](docs/09-household-budgeting.md) | Setup, schedules, publication, and item management |
| [CSV import](docs/10-csv-transaction-import.md) | Mapping, review, multi-account sessions, and transfer matching |
| [Roadmap](docs/11-roadmap.md) | Release preparation and post-v1 work |
| [Account reconciliation](docs/12-account-reconciliation.md) | Statement checkpoints and later-change review flags |
| [Budget actuals](docs/13-budget-actuals.md) | Assignments, forecasts, and potential savings |
| [Recurring detection and alerts](docs/14-recurring-detection.md) | Manual/daily scans, review, and unread status |
| [Merchant/category rules](docs/15-merchant-category-rules.md) | Rule management and import suggestions |
| [Saved CSV mappings](docs/16-saved-csv-mappings.md) | Reusable bank layouts and confirmation rules |
| [Available cash outlook](docs/17-available-cash-outlook.md) | Selected cash/card balances and remaining commitments |
| [Import undo/correction](docs/18-import-undo-correction.md) | Atomic undo, payments, transfers, reconciliation audit |
| [Savings funds](docs/19-savings-funds.md) | Account makeup, component goals, funding and budget integration |
| [Account security](docs/20-account-security.md) | Passwords, sessions, recovery codes, SMTP configuration |
| [Hybrid storage and Azure](docs/21-hybrid-storage-and-azure.md) | Implemented split runtime and remaining delivery gates |
| [Migration/recovery evidence](docs/22-migration-recovery-rehearsal.md) | Synthetic and private-copy verification boundaries |
| [Household backups/export](docs/23-household-backup-restore-export.md) | Encrypted downloads, restore safety, portability, CSV |
| [Frontend commands](Coiny.Web/README.md) | Angular commands, configurations, source layout |
| [Backend tests](Coiny.API.Tests/README.md) | Regression and PostgreSQL integration coverage |
| [Operator runbook](deploy/operations/README.md) | Maintenance, offline migration, installation backups/recovery |
| [Recovery lab](deploy/recovery-lab/README.md) | Independent fresh-instance restore rehearsal |

## Verification

The latest local verification passed **386 frontend tests**, **290 backend regression
tests**, the frontend container build, and both local lab smoke checks. A separate
focused run passed **21 PostgreSQL/API integration checks plus two crypto/CSV unit
tests**; five operator packaging tests run during integration-image builds.
The old Angular starter-test failure is fixed. There is no automated browser
end-to-end suite; local smoke checks and the manual portable restore complement
unit/integration coverage. See [verification details](docs/06-development-setup.md#verification).

## Next milestones

Complete a final release review, production Compose/proxy/secret configuration,
Azure VM and domain/HTTPS setup, scheduled offsite backups with tested recovery,
monitoring, and update/rollback instructions before a small tester rollout.
No Azure resources or public deployment have been created by the local rehearsals.
