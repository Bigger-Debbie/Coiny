# Coiny local Docker test group

This is a persistent **`coiny-test`** Compose project in Docker Desktop. It runs the
real Angular UI and API behind Caddy at **http://localhost:6080**. It binds only to
127.0.0.1, uses synthetic demo data and mounts no host financial database.

The installed test group now runs the experimental hybrid mode: PostgreSQL owns
identity, sessions, household membership and orchestration; each household has its
own SQLite financial file under `/data/households` in `coiny-test_app-data`.
The two existing test users and households were migrated and verified. The original
combined database and a pre-cutover snapshot are retained in that volume. This is
still a local integration environment, not production-ready hosting. The original
`coiny-hybrid-lab` project remains a separate architecture experiment.

## Deploy an increment

From the `feature/hybrid-hosting` worktree root:

```sh
python3 deploy/docker-test/test-stack.py up
```

This generates private test credentials once, builds both images from this
worktree, starts/recreates the services and checks the UI, SPA deep links, API
routing, login, synthetic accounts and logout/session revocation. No registry push
is required locally. Images are tagged `coiny-test-api:local` and
`coiny-test-web:local`. Development servers on 4200/5298 and the lab on 6088 remain
independent. Only one worktree should deploy this named test project at a time.

Demo sign-in: **alex@coiny.test** / **CoinyDemo!2026**. These are public, synthetic
demo credentials, never production credentials. The API uses Development mode only
to support the existing explicit demo seed command; this Compose file must not be
used for public deployment. There is no SMTP configured and no publicly trusted
TLS certificate for localhost. Azure packaging and trusted proxy/HTTPS hardening
are separate planned work.

```sh
python3 deploy/docker-test/test-stack.py status
python3 deploy/docker-test/test-stack.py smoke
python3 deploy/docker-test/test-stack.py logs
python3 deploy/docker-test/test-stack.py down
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
  migration command and rehearsal.
- Never copy real financial data into this environment without an explicit private
  migration rehearsal. No automatic destructive reset command is provided.

## Real hybrid storage integration checks

```sh
python3 deploy/docker-test/test-stack.py integration
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
python3 deploy/docker-test/test-stack.py migrate-hybrid
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

The integration command also runs the [migration/recovery rehearsal](../../docs/22-migration-recovery-rehearsal.md):
a rich synthetic budget is migrated with injected interruptions, then real
PostgreSQL dumps and SQLite backup artifacts are recovered into temporary stores.
This does not back up, overwrite or reset the running test users. There is no
public restore endpoint or production backup scheduling in this increment.

The integration image also installs `age` and Python 3 for encrypted backup rehearsal.
It runs five operator packaging tests during build; the full recovery test restores
PostgreSQL and household snapshots from a verified encrypted bundle. The application
runtime image does not include these operator tools. See
[offline operations](../operations/README.md). No personal data is used.

Maintenance controls: `maintenance-status`, `maintenance-pause`,
`maintenance-resume`, and `maintenance-smoke` are additional `test-stack.py` actions.
Pause drains participating work and then stops the API; resume uses the private
ignored operation marker, starts the API and smoke-tests. The full smoke action
also proves an API restart stays paused. Failed operations retain the marker for
operator review. See the [maintenance runbook](../operations/README.md#maintenance-admission-draining-and-stop-before-backup).
Apply the new control migration with `migrate-hybrid` before using these actions;
an already completed migration preserves the existing split users/financial data.

Settings → Backup & export now provides the user-facing household tools. The
integration image includes three owner/restore/export HTTP tests and two encryption
unit tests in addition to the existing migration/recovery cases. These use temporary
synthetic databases; deployment smoke does not apply a restore to the persistent
local demo or private review households. See
[household backup/restore/export](../../docs/23-household-backup-restore-export.md).

The old persistent synthetic lab was retired when the [fresh recovery lab](../recovery-lab/README.md) was created. Its old volumes and local initialization selectors/credentials were removed. `integration` can recreate an isolated test PostgreSQL project on demand; `up` starts a fresh synthetic demo again. Do not confuse these disposable synthetic stores with the recovery lab once a user imports personal data.
