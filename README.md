# Coiny

Coiny is a household finance app for understanding account balances, recent activity,
and changes in net worth. It is built with ASP.NET Core, Entity Framework Core,
SQLite, ASP.NET Core Identity, JWT authentication, and Angular.

## Current status

Coiny has a working dashboard, accounts page, and household budget setup wizard.
It is in active local development. Budget forecasts are available through one year;
password recovery and complete expired-session handling remain future work.

| Area | Implemented | Still planned |
| --- | --- | --- |
| Authentication | Registration/login APIs and screens, branded login screen with animated coins and password toggle, shared profile, personal net-worth settings, local logout, token storage and route guard | Password recovery, complete expired-session handling |
| Households | Create/join setup, member-card modal, owner removal and expiring invitation codes, household-scoped financial services | Role changes, leaving/switching, email/text invitations |
| Financial data | Create/list APIs, scoped trends and category breakdown; transaction edit/delete with recalculated history; manual transfers; CSV mapping, review/bulk categorization, duplicate checks, optional bank balance verification, atomic multi-account import and transfer matching, import history with review/correction and atomic undo, and account/category management and household merchant/category rules and saved CSV mappings | XLS/XLSX imports |
| History | Opening-balance dates, daily closing snapshots, backdated recalculation, reusable trends, statement reconciliation with review flags | Intraday history and account-balance forecasts |
| Dashboard | Interactive net-worth chart, mortgage/other-debt breakdown, account monthly trends, recent transactions, personalized greeting | Further reporting and navigation destinations |
| Planning | Monthly budget setup, income-period outlook with period-surplus/available-cash views and allocation donut, breakdown, calendar, bill/contribution and goal set-aside tracking, and planned/actual/forecast allocation graphs, running pay-period remainders through one year, manual and optional daily recurring-expense scans, household alerts with personal unread badges, and review, and comparisons with category defaults and transaction assignments | Savings funds, individual account-balance forecasts |

## Get started

Follow the [local setup guide](docs/06-development-setup.md) to configure the API,
install dependencies, and start both applications. For a populated dashboard, use
[development demo data](docs/05-demo-data.md).

Default local URLs:

- Frontend: `http://localhost:4200`
- API: `http://localhost:5298/api`
- Development OpenAPI document: `http://localhost:5298/openapi/v1.json`

## Repository layout

```text
Coiny.API/       ASP.NET Core API, domain models, services, EF migrations
Coiny.API.Tests/ Repeatable backend recurrence, workflow, and migration tests
Coiny.Web/       Angular app, shared UI, styles, and frontend tests
Coiny.slnx      .NET solution
docs/           Product, domain, behavior, setup, and API documentation
```

The API targets .NET 10; the frontend uses Angular 21 and TypeScript. Exact
package versions are in [Coiny.API.csproj](Coiny.API/Coiny.API.csproj),
[package.json](Coiny.Web/package.json), and the frontend lockfile.

## Documentation

| Guide | Purpose |
| --- | --- |
| [Project vision](docs/00-project-vision.md) | Audience, product goals, and future direction |
| [Development principles](docs/01-development-principles.md) | Engineering and collaboration conventions |
| [Domain blueprint](docs/02-domain-blueprint.md) | Implemented entities, relationships, and planned concepts |
| [Balances, snapshots, and trends](docs/03-account-trends.md) | Financial rules and service responsibilities |
| [Authentication and user profile](docs/04-user-profile.md) | Current authentication behavior and personalization |
| [Demo data](docs/05-demo-data.md) | Development login, seed command, and database reset |
| [Development setup](docs/06-development-setup.md) | Configuration, startup, checks, and troubleshooting |
| [API and Postman reference](docs/07-api-reference.md) | All current endpoints, payloads, and testing workflow |
| [Dashboard and frontend](docs/08-dashboard.md) | UI behavior, component structure, and date ranges |
| [Household budgeting](docs/09-household-budgeting.md) | Agreed budget/schedule rules, schema foundation, and delivery sequence |
| [CSV transaction import](docs/10-csv-transaction-import.md) | Agreed import workflow, parsing foundation, and delivery sequence |
| [Product roadmap](docs/11-roadmap.md) | Remaining work and improvements to existing features |
| [Account reconciliation](docs/12-account-reconciliation.md) | Statement checks, verified checkpoints, and later-change review flags |
| [Budget actuals](docs/13-budget-actuals.md) | Posting-month comparisons, category defaults, and transaction assignments |
| [Recurring detection and alerts](docs/14-recurring-detection.md) | Manual/daily scans, review decisions, household settings, and personal unread status |
| [Merchant/category rules](docs/15-merchant-category-rules.md) | Rule management, preview examples, and import suggestions |
| [Saved CSV mappings](docs/16-saved-csv-mappings.md) | Reusable household bank layouts, header matching, and import confirmation rules |
| [Available cash outlook](docs/17-available-cash-outlook.md) | Period surplus and a forecast based on selected cash and credit-card balances |
| [Import undo/correction](docs/18-import-undo-correction.md) | Import review, atomic undo, transfer/payment handling, and reconciliation audit history |
| [Frontend commands](Coiny.Web/README.md) | Angular-specific commands and source layout |

## Next milestones

Household budgeting, dated item editing, planned/actual comparisons, pay-period
forecasts, recurring-expense detection, and household alerts are implemented.
Signup/household setup, account/category management, transaction corrections,
reconciliation, and multi-account CSV imports are also implemented.

Next areas include savings funds, backup/restore and export, import correction, and authentication/session improvements. See the
[roadmap](docs/11-roadmap.md) for the remaining work.

For current development limitations and test status, see the
[setup guide](docs/06-development-setup.md#verification).

CSV import supports multi-account sessions with independent file mapping, per-account spreadsheet review, explicit transfer matching across files or existing entries, and one atomic final save. The same flow handles a single file, hiding file-switching controls and skipping transfer matching when there are no suggestions.
