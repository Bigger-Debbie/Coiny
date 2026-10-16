# Development Setup

[Documentation index](../README.md#documentation)

Commands below assume macOS/Linux with a POSIX-style shell and start from the
repository root, unless a different directory is shown. They do not depend on a
specific editor.

## Prerequisites

- .NET 10 SDK for `Coiny.slnx` and `net10.0`.
- EF Core CLI 10.x; the project's EF packages currently use 10.0.9.
- Node.js satisfying the installed Angular CLI's engine range:
  `^20.19.0 || ^22.12.0 || >=24.0.0`.
- npm; `Coiny.Web/package.json` records npm 10.9.7 as its package manager.

Check the local tools:

```sh
dotnet --version
dotnet ef --version
node --version
npm --version
```

If the EF CLI is absent, install the version matching the current EF packages:

```sh
dotnet tool install --global dotnet-ef --version 10.0.9
```

## Configure the API

The project already has a `UserSecretsId`; no `user-secrets init` is required.
Set a local JWT signing key before starting the API or invoking EF commands that
build its application host. Replace the placeholder below with a generated
random key of at least 32 ASCII characters. `openssl rand -hex 32` can generate one.

```sh
dotnet user-secrets set "Jwt:Key" "<your-generated-signing-key>" --project Coiny.API
```

Checked-in `appsettings.json` supplies:

| Setting | Default |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | `Data Source=coiny.db` |
| `Jwt:Issuer` | `Coiny` |
| `Jwt:Audience` | `CoinyUsers` |
| `Jwt:ExpirationMinutes` | `120` |

Do not commit the signing key. Environment variables such as `Jwt__Key` and
`ConnectionStrings__DefaultConnection` can override configuration. The examples
below use an absolute database path so EF and the API consistently use the same
file regardless of their working directory.

## Restore and initialize

```sh
dotnet restore Coiny.slnx
export ConnectionStrings__DefaultConnection="Data Source=$PWD/Coiny.API/coiny.db"
```

Choose one initialization path:

- For an empty application database, run
  `dotnet ef database update --project Coiny.API`.
- For an empty database populated with a demo household, follow
  [demo seeding](05-demo-data.md#seed-an-empty-database). Seeding applies migrations
  itself; there is no need to seed on every startup.
- For an existing database, `database update` preserves data while applying
  pending migrations. API startup does not automatically migrate it.

## Start the API

```sh
dotnet run --project Coiny.API --launch-profile http
```

The HTTP launch profile sets `Development` and listens on
`http://localhost:5298`. The API base is `/api`. Development exposes an OpenAPI
JSON document at `/openapi/v1.json`; no Swagger UI is configured.

An alternative `https` launch profile also listens on `https://localhost:7191`.
Use a trusted local development certificate when choosing HTTPS and update the
frontend API URL accordingly. The API includes HTTPS redirection middleware;
keep client and launch-profile URLs consistent when diagnosing redirects.

## Start the frontend

In another terminal:

```sh
cd Coiny.Web
npm ci
npm start
```

Open `http://localhost:4200/login`. For a seeded database, use the
[demo login](05-demo-data.md#development-only-login).

`src/environments/environment.ts` currently exports `environemnt` and points to
`http://localhost:5298/api`. Preserve that existing export spelling when changing
its URL. The API's development CORS policy allows `http://localhost:4200`.
Using another port/origin requires a matching CORS configuration change.

For a new user without demo data, use Postman to register, create a household,
and create initial financial data. Signup and household-setup screens are not
implemented. See the [API workflow](07-api-reference.md#postman-workflow).

## Verification

From the repository root:

```sh
dotnet build Coiny.slnx
dotnet test Coiny.API.Tests/Coiny.API.Tests.csproj
```

From `Coiny.Web`:

```sh
npm run build
npm test -- --watch=false
```

A focused dashboard/component suite is available:

```sh
npm test -- --watch=false \
  --include='src/app/features/dashboard/**/*.spec.ts' \
  --include='src/app/shared/components/trend-chart/**/*.spec.ts' \
  --include='src/app/shared/components/icon/**/*.spec.ts'
```

For accounts, category breakdown, creation modals, and shared chart controls:

```sh
npm test -- --watch=false \
  --include='src/app/features/accounts/**/*.spec.ts' \
  --include='src/app/features/transactions/create-transaction/**/*.spec.ts' \
  --include='src/app/shared/components/donut-chart/**/*.spec.ts' \
  --include='src/app/shared/components/modal/**/*.spec.ts' \
  --include='src/app/shared/components/select-menu/**/*.spec.ts'
```

Current verification baseline after the category-breakdown implementation:

- Backend and frontend production builds pass.
- The dashboard, accounts, budgeting wizard, creation-form, and shared-component tests pass.
- Budget wizard tests cover save/resume, full-draft preservation, validation, stale
  conflicts, unsaved navigation, publication, and future-only published plans.
- Budget dashboard tests cover grouped allocations, unassigned/over-budget
  presentation, separate payroll details, month navigation, retries, and stale responses.
- The full frontend run has 97 passing tests out of 98; the remaining failure
  is the known starter test described below.
- The full frontend suite has a known starter test failure in `app.spec.ts`:
  it still expects the generated “Hello, Coiny.Web” heading.
- Existing backend build warnings include nullable properties in `HouseholdMember`.
- Dashboard and accounts CSS exceed their 4 kB warning budgets (about 4.03 kB
  and 4.25 kB respectively); the production build still passes.
- The backend suite has 48 passing xUnit tests for recurrence, household-scoped
  draft/publication workflows, optimistic conflicts, atomic rollback, and
  migration data preservation, and HTTP contracts/access checks. These use isolated in-memory SQLite databases.
  See the [backend test guide](../Coiny.API.Tests/README.md).
- There is no end-to-end browser suite yet. Earlier temporary backend checks are
  separate from the repository tests; `dotnet build` does not run either.
- Budget foundation verification used 29 isolated SQLite schema checks, including
  migration apply/rollback/reapply, preservation of preexisting financial rows,
  draft/revision constraints, recurrence fields, and exact decimal round trips.
  See [household budgeting](09-household-budgeting.md) for implemented boundaries.

## Troubleshooting and repository housekeeping

| Symptom | Check |
| --- | --- |
| Missing JWT configuration/key | Configure `Jwt:Key`; use the Development launch profile for user secrets |
| SQLite reports a missing table/column | Stop the API, apply migrations to the same connection string, restart |
| Login fails after a reset | Log out through the header menu (or remove the old local-storage token) and use the newly seeded credentials |
| Dashboard errors for a new registration | Create the user's household before financial API calls |
| Account trends missing after an API change | Restart the API so its response includes `monthlyTrend` |
| Category breakdown returns 404 after updating | Restart the API to load `GET /api/transactions/category-breakdown` |
| Frontend cannot reach API | Confirm launch URL, `environment.ts`, and allowed CORS origin |

Tracked API `bin/` and `obj/` files currently generate noisy Git changes after a
build. Keep them out of feature commits. Local SQLite sidecars (`coiny.db-wal`,
`coiny.db-shm`) and `.DS_Store` may also appear as untracked files. Ignore-rule and
tracked-artifact cleanup is still separate work; this guide does not remove them.
