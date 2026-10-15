# Development Demo Data

The demo seed command runs explicitly, exits after seeding, and is unavailable
outside `Development`. Normal API startup never creates sample data. It applies
pending migrations and requires an empty database; running it again refuses to
change existing data.

From the repository root, seed an empty local database:

```sh
dotnet run --project Coiny.API --launch-profile http -- --seed-demo
```

For a complete reset, stop the API first. The following commands permanently
delete the local test database and recreate it with demo data:

```sh
dotnet ef database drop --force --project Coiny.API
dotnet run --project Coiny.API --launch-profile http -- --seed-demo
```

Use the same SQLite connection string for both commands. The local database is
`Coiny.API/coiny.db`. An explicit absolute `ConnectionStrings__DefaultConnection`
environment variable can select a separate disposable database for testing.

Development-only login:

- Email: `alex@coiny.test`
- Password: `CoinyDemo!2026`
- First name: Alex
- Household name: Ross

The seed creates checking, emergency savings, retirement, and credit-card accounts
with nonzero opening balances. History starts on the first day of the month six
months before today, using the application's America/New_York calendar day.
Paychecks, rent, bills, groceries, dining, fuel, savings interest, and employer
retirement contributions provide sample changes across chart ranges. Daily
coffee purchases ensure activity yesterday and today. Amounts and date patterns
are deterministic; a later reset moves the history window to the new current day.

Identity creates the user. Household, institution, category, account, and
transaction services create the financial data and snapshots. Transactions are
synthetic examples, not financial forecasts or a complete accounting scenario;
paired transfers and credit-card payments are omitted until supported.

Start the API normally after seeding. Log out of any old browser session and log
in with the demo credentials, since tokens from deleted users are no longer valid.
