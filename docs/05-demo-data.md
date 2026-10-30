# Development Demo Data

Complete [local configuration](06-development-setup.md#configure-and-start) first.
The seed command is explicit, runs only in `Development`, applies pending
migrations, and exits without starting the HTTP server. Normal API startup never
creates sample data. A populated database is rejected rather than overwritten.

The current seed does not create a household budget. On a fresh seed, migrations
create the budget tables, but the household has no budget revisions, income
sources, or allocations yet.

This guide applies to the default legacy `CoinyContext` development database.
Hybrid household provisioning uses its own migration chain; the fresh recovery
lab does not seed demo users. Never use these reset commands against a retained
personal review or recovery installation. See [setup modes](06-development-setup.md#hybrid-storage-and-test-helpers).

## Seed an empty database

Run from the repository root. An explicit connection string ensures the CLI and
API target the same local file:

```sh
export ConnectionStrings__DefaultConnection="Data Source=$PWD/Coiny.API/coiny.db"
dotnet run --project Coiny.API --launch-profile http -- --seed-demo
```

## Reset and seed

Stop the API first. These commands **permanently delete all data** in the named
local test database, recreate its schema, and seed it:

```sh
export ConnectionStrings__DefaultConnection="Data Source=$PWD/Coiny.API/coiny.db"
dotnet ef database drop --force --context CoinyContext --project Coiny.API
dotnet run --project Coiny.API --launch-profile http -- --seed-demo
```

Start the API normally afterward:

```sh
dotnet run --project Coiny.API --launch-profile http
```

Log out through the header profile menu and sign in again. Deleted users' old
tokens cannot access the newly created demo household. If the old page is
unavailable, remove `coiny_token` from this site's local storage using browser
developer tools, then visit `/login`.

## Development-only login

| Field | Value |
| --- | --- |
| Email | `alex@coiny.test` |
| Password | `CoinyDemo!2026` |
| First name | Alex |
| Household name | Ross |

These are synthetic development credentials, defined in `DemoDataSeeder`.
They are not intended for a deployed environment.

## Sample financial history

| Account | Type | Opening balance |
| --- | --- | --- |
| Primary Checking | Checking | $6,000 |
| Emergency Savings | Savings | $12,000 |
| 401(k) | Retirement | $75,000 |
| Everyday Credit Card | CreditCard | $1,800 owed |

The seed creates three institutions and nine income/expense categories. History
begins on the first day of the month six months before today, using the
America/New_York calendar day, and ends today.

Paychecks, rent, utilities, groceries, dining, fuel, savings interest, retirement
fees, and employer contributions produce changes across chart ranges. Daily
coffee purchases ensure activity for yesterday/today and weekly comparisons.
Amounts and schedules are deterministic; dates and row counts move with the day
on which the command runs.

Identity creates the user. Household, institution, category, account, and
transaction services create the financial data. Snapshots are generated through
the same atomic workflows as normal API calls. The seed restores its temporary
authenticated context when it finishes.

This is a useful UI/behavior fixture, not complete household accounting. Paired
transfers and credit-card payments are omitted from this seed. The transfer backend
and Add Transaction modal support them separately; CSV import and explicit
transfer matching are also implemented. This seed simply does not exercise them.
To test in isolation, set `ConnectionStrings__DefaultConnection` to an absolute
SQLite path in a separate disposable directory.
