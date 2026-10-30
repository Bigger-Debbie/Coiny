# Coiny local Docker test group

Creates a synthetic demo at `http://localhost:6080`. It binds to loopback and
does not mount the development database. Use [Docker](../../../Docker/README.md)
for self-hosting; this is a Development test fixture.

A fresh `up` seeds combined SQLite. `migrate-hybrid` converts only that synthetic
store to PostgreSQL/per-household SQLite. Later starts preserve the chosen mode.
The integration command can run without the demo UI/API.

## Run

From the repository root (`main`):

```sh
python3 tools/testing/docker-test/test-stack.py up
```

This generates private test credentials once, builds both images from this
checkout, starts/recreates the services and checks the UI, SPA deep links, API
routing, login, synthetic accounts and logout/session revocation. No registry push
is required locally. Images are tagged `coiny-test-api:local` and
`coiny-test-web:local`. Development servers on 4200/5298 and the retained
review/recovery labs on 6081/6082 remain independent. Only one worktree should deploy this named test project at a time.

Demo sign-in: **alex@coiny.test** / **CoinyDemo!2026**. These are public, synthetic
demo credentials, never production credentials. The API uses Development mode only
to support the existing explicit demo seed command; this Compose file must not be
used for public deployment. There is no SMTP configured and no publicly trusted
TLS certificate for localhost. Azure packaging and trusted proxy/HTTPS hardening
are separate planned work.

```sh
python3 tools/testing/docker-test/test-stack.py status
python3 tools/testing/docker-test/test-stack.py smoke
python3 tools/testing/docker-test/test-stack.py logs
python3 tools/testing/docker-test/test-stack.py down
```

`down` stops/removes containers and retains named volumes. Redeploy keeps test
changes, JWT configuration, Data Protection keys and PostgreSQL state. The seed
runs only on the first empty application volume and refuses to overwrite an
existing database after an interrupted seed. Do not delete `.env` while retaining
the PostgreSQL volume; regenerated passwords will not update existing PostgreSQL
credentials. Keep `.env` private (generated with mode 0600).

## Boundaries

- `web`: Caddy plus Angular assets; only published port is 127.0.0.1:6080.
- `api`: internal 8080, dedicated household SQLite and Data Protection volumes.
- `postgres`: internal 5432, dedicated control-data volume.
- Caddy does not mount application financial files or PostgreSQL storage.
- Docker build contexts exclude DB files, credentials, local development config,
  generated API output and frontend dependency/build directories.
- A fresh legacy-mode volume is seeded synthetically. Hybrid-mode provisioning
  applies its separate SQLite migration chain; control migrations must be applied
  deliberately before startup. Future release upgrades still need an operational
  upgrade/release command and rehearsal beyond the implemented initialization and
  legacy-copy tools.
- Never copy real financial data into this environment without an explicit private
  migration rehearsal. No automatic destructive reset command is provided.

## Real hybrid storage integration checks

```sh
python3 tools/testing/docker-test/test-stack.py integration
```

The optional `integration` profile builds a .NET test runner, migrates disposable
PostgreSQL databases using Coiny's real control model and creates household SQLite
files using Coiny's real financial migration set. It tests Identity password
storage, table isolation, session/membership revocation, metadata validation,
provisioning recovery, missing storage, node ownership and catalog failure. Each
test deletes only its own generated `coiny_it_*` database and temporary files.
The runner refuses a connection not targeting the `coiny-test` PostgreSQL service.
It does not change the UI's household files or central user data. The integration
checks use temporary databases, independent of the running test API.

## Explicit Docker test cutover and recovery

```sh
python3 tools/testing/docker-test/test-stack.py migrate-hybrid
```

For a fresh installation, run `up` first to create synthetic demo data, then run
this explicit cutover. It builds the API, pauses only the test API, makes a
consistent backup, copies central and household data, verifies rows and decimal
values, preserves ID high-water marks and restarts in hybrid mode. Existing
passwords and account IDs are preserved; old sessions are revoked, so sign in
again. The original `/data/coiny-test.db` and `/data/coiny-before-hybrid.db` remain
unchanged. The command refuses other database names, hosts and source paths.

An interrupted copy remains resumable; the API stays stopped on failure. Never
restart the original database to accept new writes during an incomplete migration.
If the original changed after its snapshot, migration refuses to activate stale
data. A completed migration is idempotent and never reimports over new split data.

The ignored local `.hybrid-enabled` selector makes later `up` commands use the
hybrid Compose override. A durable `.hybrid-migration-started` marker in the named
volume additionally prevents stale legacy startup if that worktree selector is
lost. Run `migrate-hybrid` again to recover the selector; completed data is kept.
Do not remove these guards to switch back after new writes.

Same-household HTTP writes and recurring jobs share a writer gate. Recurring jobs
open explicit registered household contexts. Combined preference changes use a
central pending operation plus a local receipt; retries finish either commit
without incrementing the recurring version twice. Pending changes are retried by
the worker, and a stale version cancels the intent for review.

Integration checks include resumable migrations, financial comparisons, PostgreSQL
dump/restore, household snapshots, and encrypted packaging. They use disposable
test databases and do not restore over persistent review installations.

Maintenance controls: `maintenance-status`, `maintenance-pause`,
`maintenance-resume`, and `maintenance-smoke` are additional `test-stack.py` actions.
Pause drains participating work and then stops the API; resume uses the private
ignored operation marker, starts the API and smoke-tests. The full smoke action
also proves an API restart stays paused. Failed operations retain the marker for
operator review. See the [maintenance runbook](../../operations/README.md#maintenance-admission-draining-and-stop-before-backup).
Apply the new control migration with `migrate-hybrid` before using these actions;
an already completed migration preserves the existing split users/financial data.

Settings → Backup & export now provides the user-facing household tools. The
integration image includes three owner/restore/export HTTP tests and two encryption
unit tests in addition to the existing migration/recovery cases. These use temporary
synthetic databases; deployment smoke does not apply a restore to the persistent
local demo or private review households. See
[household backup/restore/export](../../../docs/23-household-backup-restore-export.md).
