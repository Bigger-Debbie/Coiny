# Coiny

Coiny is a household finance app for understanding account balances, recent activity,
and changes in net worth. It is built with ASP.NET Core, Entity Framework Core,
SQLite, ASP.NET Core Identity, JWT authentication, and Angular.

## Current status

Coiny has a working dashboard, accounts page, and household budget setup wizard.
It is in active local development; forecasting and the complete authentication
experience remain future work.

| Area | Implemented | Still planned |
| --- | --- | --- |
| Authentication | Registration/login APIs and screens, branded login screen with animated coins and password toggle, shared profile, personal net-worth settings, local logout, token storage and route guard | Password recovery, complete expired-session handling |
| Households | Create/join setup, member-card modal, owner removal and expiring invitation codes, household-scoped financial services | Role changes, leaving/switching, email/text invitations |
| Financial data | Create/list APIs, scoped trends and category breakdown; transaction edit/delete with recalculated history; manual transfers; CSV mapping, review/bulk categorization, duplicate checks, optional bank balance verification, atomic import and history | Transfer matching UI, account/category management, saved bank mappings |
| History | Opening-balance dates, daily closing snapshots, backdated recalculation, reusable trends | Intraday history and forecasting |
| Dashboard | Interactive net-worth chart, mortgage/other-debt breakdown, account monthly trends, recent transactions, personalized greeting | Further reporting and navigation destinations |
| Planning | Budget schema, recurrence, draft/preview/publish API, setup wizard, monthly allocation donut, breakdown, income-period bills, and calendar | Goals, forecasting |

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
| [Frontend commands](Coiny.Web/README.md) | Angular-specific commands and source layout |

## Next milestones

The dashboard, login screen, profile menu/local logout, and accounts page with
interactive income/expense category breakdown are implemented. Household budgeting
now has models, recurrence, a draft/preview/publish API, and a resumable frontend
setup wizard and published monthly allocation dashboard. The calendar highlights
the current income period and scheduled items. Next are richer budget editing
and reporting.
Signup and create/join household setup are implemented. Transaction corrections and CSV imports are implemented. Account/category management,
password recovery, and complete expired-session handling remain future work.

For current development limitations and test status, see the
[setup guide](docs/06-development-setup.md#verification).

CSV import supports multi-account sessions with independent file mapping, per-account spreadsheet review, explicit transfer matching across files or existing entries, and one atomic final save. The same flow handles a single file, hiding file-switching controls and skipping transfer matching when there are no suggestions.
