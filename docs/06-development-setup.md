# Local development

For a self-hosted installation, use [Docker](../Docker/README.md). These commands
run the development API and Angular separately with a combined SQLite database.

## Requirements

- .NET SDK 10 and EF CLI 10.x (EF packages use 10.0.9).
- Node.js compatible with Angular 21: `^20.19.0 || ^22.12.0 || >=24.0.0`.
- npm (`package.json` specifies 10.9.7).

```sh
dotnet tool install --global dotnet-ef --version 10.0.9
dotnet restore Coiny.slnx
```

If EF CLI is already installed, check its version instead of installing it again.

## Configure and start

From the repository root, generate a JWT key with `openssl rand -hex 32` and store it:

```sh
dotnet user-secrets set "Jwt:Key" "YOUR_GENERATED_KEY" --project Coiny.API
export ConnectionStrings__DefaultConnection="Data Source=$PWD/Coiny.API/coiny.db"
dotnet ef database update --context CoinyContext --project Coiny.API
dotnet run --project Coiny.API --launch-profile http
```

Use the same connection string when migrating and starting the API. For a new
sample household, follow [demo seeding](05-demo-data.md) instead; seeding applies
migrations and refuses an existing database. Back up populated databases before
applying migrations. Startup does not migrate them automatically.

In another terminal:

```sh
cd Coiny.Web
npm ci
npm start
```

| Service | URL |
| --- | --- |
| Angular | `http://localhost:4200` |
| API | `http://localhost:5298/api` |
| Development OpenAPI JSON | `http://localhost:5298/openapi/v1.json` |

The HTTPS launch profile also listens on 7191. Changing origins requires matching
frontend API URL and API CORS configuration. The environment export is currently
spelled `environemnt`; preserve that spelling when changing its URL.

New users register and create/join a household. Owners generate join codes under
**Settings → Household**. SMTP is optional; see [security](20-account-security.md).

## Verification

```sh
dotnet build Coiny.slnx
dotnet test Coiny.API.Tests/Coiny.API.Tests.csproj
cd Coiny.Web
npm run build
npm test -- --watch=false
```

Tests do not use the personal development database. PostgreSQL tests require
explicit test configuration and otherwise skip; see [backend tests](../Coiny.API.Tests/README.md).
Frontend tests use Vitest/jsdom; no browser end-to-end runner is configured.
Production build budgets can emit warnings; see `angular.json` for limits.

## Hybrid storage and test helpers

Hybrid mode uses PostgreSQL control storage and household SQLite files. It must be
explicitly enabled and initialized or migrated; never point its household root at
the combined development database. Design-time split-context factories are for
scaffolding, not deployed connection settings. See [architecture](21-hybrid-storage-and-azure.md).

Development-only helpers live under [tools/testing](../tools/testing/README.md).
Existing private review (6081) and recovery (6082) stores contain personal data;
they are not disposable test fixtures. Their credentials stay outside Git.

## Troubleshooting

| Problem | Check |
| --- | --- |
| Missing JWT key | Set the user secret and use Development mode |
| Missing SQLite table/column | Stop API, migrate the same database with `--context CoinyContext`, restart |
| Stale token after reset/upgrade | Sign out and sign in again |
| Financial request fails for a new user | Complete household setup |
| Frontend cannot reach API | Check launch URL, environment API URL, and CORS origin |
| EF reports multiple contexts | Specify `--context CoinyContext` for combined SQLite |

Keep local databases/sidecars, `.env`, and generated output out of commits. The
repository still tracks older API `bin/` and `obj/` files; exclude their build noise
until that separate cleanup is done.
