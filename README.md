# Coiny

A household finance app for accounts, spending, budgets, savings goals, and net worth.
Built with ASP.NET Core / EF Core 10 and Angular 21.

## Features

- Accounts, balance history, transfers, and statement reconciliation.
- Multi-account CSV imports, category rules, saved mappings, corrections, and undo.
- Monthly and paycheck budgets, actuals, forecasts, and available-cash estimates.
- Savings funds with component goals, contribution schedules, and affordability checks.
- Recurring-expense detection and household alerts.
- Shared households, password recovery, offline recovery codes, and session controls.
- Encrypted household backups, portable restores, and CSV export.

## Run Coiny

**Self-hosting:** start with [Docker/README.md](Docker/README.md). Compose runs
Caddy/web, the API, and PostgreSQL. Each household's finances have a separate SQLite
file. The images build locally; no published image registry is configured.

**Development:** follow [local setup](docs/06-development-setup.md).
The default uses one combined SQLite database, an API at `http://localhost:5298/api`,
and Angular at `http://localhost:4200`. Updating the code does not migrate existing data.

Coiny processes financial data on the server. Separate household files are not
end-to-end encryption. See [storage architecture](docs/21-hybrid-storage-and-azure.md).

## Documentation

| Topic | Guides |
| --- | --- |
| Setup | [Development](docs/06-development-setup.md), [demo data](docs/05-demo-data.md), [self-hosting](Docker/README.md), [operations](Docker/operations.md) |
| Accounts | [Balances and trends](docs/03-account-trends.md), [reconciliation](docs/12-account-reconciliation.md), [frontend](docs/08-dashboard.md) |
| Budget | [Setup and schedules](docs/09-household-budgeting.md), [actuals and forecasts](docs/13-budget-actuals.md), [available cash](docs/17-available-cash-outlook.md), [savings funds](docs/19-savings-funds.md) |
| Imports | [CSV](docs/10-csv-transaction-import.md), [merchant rules](docs/15-merchant-category-rules.md), [saved mappings](docs/16-saved-csv-mappings.md), [undo](docs/18-import-undo-correction.md) |
| Household | [Users and membership](docs/04-user-profile.md), [security](docs/20-account-security.md), [recurring detection](docs/14-recurring-detection.md), [backups and export](docs/23-household-backup-restore-export.md) |
| Development | [API reference](docs/07-api-reference.md), [model](docs/02-domain-blueprint.md), [backend tests](Coiny.API.Tests/README.md), [frontend commands](Coiny.Web/README.md), [principles](docs/01-development-principles.md) |
| Project | [Vision](docs/00-project-vision.md), [roadmap](docs/11-roadmap.md), [architecture](docs/21-hybrid-storage-and-azure.md) |

## Repository

```text
Coiny.API/        API, models, services, migrations
Coiny.API.Tests/  Backend tests
Coiny.Web/        Angular frontend
Docker/          Self-hosting Compose, images, setup and operations
tools/           Migration/backup utilities and development test helpers
docs/            Feature and developer guides
```

## Release status

The v1 feature scope is complete. Local migration and cross-installation recovery
have been tested. Public hosting still needs Azure setup, scheduled offsite backups,
monitoring, and a reviewed live-data cutover. See the [roadmap](docs/11-roadmap.md).
