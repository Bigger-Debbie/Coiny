# Backend tests

From the repository root, with .NET SDK 10:

```sh
dotnet restore Coiny.slnx
dotnet test Coiny.API.Tests/Coiny.API.Tests.csproj
```

Most tests use isolated in-memory SQLite databases. They do not use or reset the
development database or require a running API/JWT configuration. Account-security
checks use real Identity/JWT middleware and a local SMTP capture service; no real
email is sent.

## Coverage

| Area | Main checks |
| --- | --- |
| Ledger | Balances, snapshots, dates, transfers, edits/deletes, rollback and ownership |
| Budget | Recurrence, draft/publication, dated changes, actuals/forecasts, payments and assignments |
| Savings | Goal rounding, affordability, fund allocations, activity/corrections and period skipping |
| Imports | CSV validation, duplicates, multi-file atomic commits, matching, idempotency and undo |
| Reconciliation | Statement checkpoints, fingerprints, invalidation history and corrected balances |
| Household | Invitation/member permissions, concurrent joins and isolation |
| Security | Login lockout/rates, sessions, password reset/change, offline codes and trusted proxy headers |
| Recovery | Encryption, schema validation, preview conflicts, safety copies and portable restore |

## PostgreSQL/SQLite integration

```sh
python3 tools/testing/docker-test/test-stack.py integration
```

This builds the Docker runner, starts a test PostgreSQL service, and runs the
real-provider suites against generated disposable databases and household files.
It covers split-model ownership, provisioning, routing, revocation, interrupted
migration, decimal preservation, maintenance, and encrypted full/household recovery.
Tests skip without `COINY_TEST_POSTGRES` and reject non-test database configuration.
The helper supplies the connection privately. Tests remove only their own stores.

The image includes PostgreSQL 17 client tools, Python, and `age`. Operator-package
tests run during its build; they can also run locally with those dependencies:

```sh
python3 -B -m unittest discover -s tools/operations -v
```

Frontend tests run separately from `Coiny.Web`. No browser end-to-end runner is
configured. See [local setup](../docs/06-development-setup.md#verification).
