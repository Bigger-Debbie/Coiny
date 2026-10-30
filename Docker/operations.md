# Running Coiny

Run these commands from `Docker`. Keep `.env`, private snapshots, and backup keys
outside Git and restrict who can read them.

## Check the installation

```sh
docker compose ps
docker compose logs --tail 100 api web postgres
```

After installation or an update, check sign-in, an account balance, the budget,
and a portable backup download. Watch disk use: old household files and restore
safety copies are retained and do not yet have automatic cleanup.

## Household backups

Owners can download encrypted backups from **Settings → Backup & export**.
Choose a backup password for a portable file, store the password separately, and
test a restore into another installation. Account password recovery cannot unlock
a forgotten backup password. Save offline account recovery codes separately too.

## Installation backups

A full recovery needs coordinated PostgreSQL and household snapshots, `.env`, and
the Data Protection key volume. Preserve Caddy state as well. Copying a running
SQLite `.db` alone can miss WAL data. Live PostgreSQL volume copies are not valid
database backups.

Use the [operator tools](../tools/operations/README.md) to pause/drain work, stop the
API, take and encrypt snapshots, and validate recovery. Store encrypted backups
off the host and keep recovery keys separately. Backup scheduling and offsite
retention must be configured by the operator; Compose does not provide them.

Maintenance uses an operation UUID. Keep it until the installation is reopened:

```sh
# Run while the API is still running; generate and record a fresh UUID first.
docker compose exec api dotnet Coiny.API.dll --maintenance pause YOUR_UUID
docker compose stop api
# Take the coordinated backup using the operator runbook.
# When ready to reopen, the stopped API releases its household storage lock:
docker compose run --rm --no-deps api --maintenance resume YOUR_UUID
docker compose up -d api
```

Do not resume until backup/restore checks finish. Keep old and recovered
installations from accepting writes at the same time.

## Update

1. Save a verified backup and record the current Git commit and image tags.
2. Read the new release's migration instructions. Initialization is not an upgrade
   command; startup refuses pending control migrations.
3. Check out the release, set a new `COINY_VERSION`, and run `docker compose build`.
4. Stop/drain the API before any required database migration; follow the release's
   instructions, then run `docker compose up -d` and verify the installation.

Do not switch back to an older database snapshot after accepting new writes.
Stop the app, preserve its latest data, and plan recovery or fix forward. There is
no automatic production upgrade/rollback service.

## Move to another host

Restore the coordinated control and household snapshots with their keys and
configuration using the operator runbook. A household portable backup replaces
only that household's finances; it does not restore installation users or sessions.
Keep the old host offline during validation. Restore keys before testing email
reset links or installation-linked backups.
