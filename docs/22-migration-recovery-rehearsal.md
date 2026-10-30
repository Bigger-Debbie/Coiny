# Hybrid migration and database recovery rehearsal

Status: synthetic and household-only private-copy migration/recovery verified locally,
2026-10-29; implementation merged into `main`. This document records the milestones
and evidence behind the [hybrid delivery plan](21-hybrid-storage-and-azure.md).
The original database was not migrated live and remains authoritative. User-facing
[backup/restore/export](23-household-backup-restore-export.md) is also implemented,
and a manual portable restore into the independent recovery lab succeeded.

General offline migration, encrypted packaging, and maintenance controls are
complete. The sections below retain historical test counts by increment; the latest
baseline is in [verification](06-development-setup.md#verification). Public hosting,
scheduled offsite backups/key custody, and final stopped-writer cutover remain.

## What now exists

`LegacyMigrationEngine` is shared by the guarded Docker migration command and
integration tests. It checks the snapshot path/digest, legacy migration history,
SQLite integrity and foreign keys, and cross-household relationships before copying
control data. A valid foreign key alone does not prove household ownership.

Durable control and household commits remain resumable. The import journal binds
all retries to the same source digest and prevents activation until verification
finishes. Each household copy is transactional and verified table by table. Decimal
columns use EF's decimal reader so historical NUMERIC affinity and current TEXT
affinity compare by value. Identity IDs, financial IDs and deleted-ID high-water
marks are preserved. Completed imports never overwrite later split data.

`HouseholdBackupArchive` is an internal operational building block. It uses the
SQLite backup API, writes private files with identity/version/checksum manifests,
and restores only into empty storage. Restore checks the registry, file metadata,
schema history, integrity, foreign keys and checksum. A staged restore is published
only after validation; an exception before publication removes its staging file.
It does not overwrite an active household file or restore central identities.

These internal operator helpers are distinct from the owner-facing upload/restore
service; the latter validates backups and replaces finance through a new file and
registry swap, rather than invoking the full-installation restore helper.
The Docker migration CLI still deliberately refuses other source paths, database
names and hosts. The general read-only migration plan/checksum-bound apply CLI
and approved private rehearsal described later are now implemented.

## Reproduce the checks

From the repository root (merged `main`):

```sh
python3 deploy/docker-test/test-stack.py integration
```

The integration profile includes PostgreSQL 17's `pg_dump` and `pg_restore` clients
from the official PostgreSQL package repository. Only the test image includes these
clients; the application runtime image is unchanged. The runner creates temporary
`coiny_it_*` / `coiny_recovery_*` databases and directories, then removes its own
artifacts. Existing Docker users and household files are not reset.

## Evidence

| Check | Verified behavior |
| --- | --- |
| Three interrupted migration boundaries | Retry succeeds after the central commit, a household commit, and immediately before activation |
| Rich financial fixture | Monthly plan, income-period plan, actuals, three-month forecast, available cash, savings account/fund makeup and reconciliation output match the original at a fixed clock |
| Historical links | Fixture includes a two-transaction bill, paired transfers, import baselines, skipped savings contribution, component goals, snapshots, rules, mappings and personal alert-read state; all financial tables are compared during migration |
| Source preservation | Original snapshot digest remains unchanged through all migration attempts |
| ID reuse protection | A new post-migration transaction receives an ID above the source's deleted-ID high-water mark; repeating the completed import keeps it |
| Invalid source ownership | A cross-household transaction/account reference is rejected before destination users or household files are created |
| Financial-only recovery | Wrong household, retargeted manifest, unsupported format/schema, path traversal, corruption and overwriting an existing destination are rejected |
| Interrupted restore | Failure just before publication leaves no final/temporary DB; retry publishes a valid file |
| Current access control | Restoring a financial file does not reinstate a removed member |
| Actual central backup/restore | `pg_dump --format=custom` and `pg_restore` restore into a new PostgreSQL database; household artifacts restore into a new directory and registry ownership is assigned to the new node |
| Recovered behavior | All fixed-clock financial outputs match; users/password hashes remain usable |
| Historical credentials | Full recovery revokes restored sessions, deletes offline recovery codes and rotates each user's security stamp; old session claims are denied |

The recovery, maintenance and real PostgreSQL/API tests now exercise these
scenarios together: **17 integration tests passed**.
The separate existing backend regression suite also passed all **288 tests**.
They test the real EF migration chains, PostgreSQL provider and financial services,
not the earlier toy-schema architecture lab.

## Full recovery versus a household restore

A household financial restore retains the current central users, memberships and
revocations. The rehearsal proves this by removing a member after backup and
checking that the restored file remains inaccessible to that member.

A full database recovery restores central state as of the backup. Membership changes
made after that point are not magically present. Before reopening a recovered
installation, invalidate historical sessions/codes/reset-token stamps and review
membership, reapplying later access removals from the operational record. Rotating
security stamps invalidates historical credentials; it does not itself remove a
membership restored from an older backup. Password hashes are preserved so users
can sign in again after review.

`RestoredControlSecurity` performs the tested database-side invalidation in one
transaction. Application JWT signing material, Data Protection keys and other
installation secrets must be restored or deliberately rotated by the deployment
runbook. This rehearsal proves database recovery and credential invalidation, not
recovery of the entire host's secret configuration or email delivery.

## What remains before hosted backup operations

- Durable maintenance admission/drain is now implemented (see below). Coordinated
  snapshot operations still require stopping all API/jobs after drain; the
  synthetic recovery fixture has no running writers for its snapshot window.
- Authenticated encrypted packaging is now implemented with age (see below).
  Production recovery-key custody, installation-secret recovery, off-VM retention
  and scheduling remain. PostgreSQL archives remain trusted restore input.
- The encrypted manifest now binds the dump/files, release and recovery-key label;
  household manifests include schema versions. Installation-secret recovery is
  explicitly separate and still needs its protected production runbook.
- Fence/stop the old installation before activating a recovered host to prevent
  divergent writes. This rehearsal does not prove automated failover or HA.
- Scheduled backups, failure/age alerts, rotation and a recovery drill on the
  actual Azure disk/filesystem. Local success is not a measured recovery SLA.
- Read-only migration plan/report and checksum-bound offline apply now exist. An
  approved private copy of real data and user-facing household backup/restore/export
  flows remain.

No Azure resources or offsite storage were created for this rehearsal.

## Primary references

- [PostgreSQL Ubuntu packages](https://www.postgresql.org/download/linux/ubuntu/): official client repository setup.
- [PostgreSQL 17 pg_dump](https://www.postgresql.org/docs/17/app-pgdump.html): custom archives, database-level scope and trusted restore input.
- [SQLite backup API](https://www.sqlite.org/backup.html): consistent snapshots without copying a live database file and ignoring WAL.

## Encrypted packaging and general offline commands

The next increment adds [operator tools and commands](../deploy/operations/README.md).
`--migration-plan` inspects a closed legacy snapshot read-only and writes a new
private count/schema/digest report without opening any destination.
`--migrate-snapshot` requires that reviewed digest and explicit offline operation,
then uses the same verified/resumable engine. This is operator tooling, not an
application endpoint or a live-source migration. It rejects active WAL/journal
state and unknown source migrations. Source and destination APIs/jobs must be stopped.
The root lock does not constitute distributed PostgreSQL maintenance fencing.

`backup_bundle.py` packages coordinated snapshots using the established `age`
executable. The encrypted manifest binds file hashes/lengths, release, recipient
and recovery-key version. Private recovery keys are kept outside the backup host;
installation secrets are explicitly excluded and require separate recovery/rotation.
Wrong keys, tampering, truncation, unsafe members and overwrites are rejected.
Completed ciphertext is published without overwriting an existing file.
Plaintext staging is private but requires encrypted disks and crash cleanup.
Encryption is not author authentication; trusted backup custody is still required.

Five real-age packaging tests pass. The real PostgreSQL recovery test now encrypts
and decrypts its central dump and all household artifacts, deletes the original
plaintext snapshots, and restores exclusively from verified decrypted output.
The general migration planning/apply check adds a thirteenth integration test,
including rejection of a digest that differs from the reviewed plan.
This supersedes the earlier statement that general CLI and encrypted packaging
were entirely absent. It does not complete maintenance fencing, offsite scheduling,
secret recovery, general export or the user-facing backup/restore feature.

## Maintenance admission and required process fencing

The [maintenance runbook](../deploy/operations/README.md#maintenance-admission-draining-and-stop-before-backup)
adds a durable central pause, shared session admission leases for all hybrid HTTP
routes, startup provisioning and the complete recurring scan pass, and an exclusive
operator drain. Admission closes before waiting for active work. Pauses survive
operator failure, API restart and cancellation, and require the matching operation
UUID to resume. Missing state fails closed. Paused HTTP requests return 503 with a
maintenance message and Retry-After, including central authentication/household routes.

This is deliberately cooperative admission/drain, not an online backup fence against
network partitions. PostgreSQL releases session locks on disconnect; an independent
SQLite task may still be executing. **Stop all API/jobs after drain and before the
coordinated snapshot or restore**, verify they are stopped, and keep them stopped
through verification. No HA/distributed fencing claim is introduced. Offsite
scheduling, host secret recovery, production packaging and Azure recovery remain.

Three additional real-provider gate tests cover concurrent leases across separate
instances, drain ordering, interrupted/cancelled operators, matching-token resume,
exclusive-holder protection and missing-state rejection. A fourth HTTP test verifies
central auth and financial routes are blocked then work with the existing session
after explicit resume. The encrypted full recovery case now restores a paused
installation and only reopens after historical credential invalidation. This brings
the PostgreSQL/API/recovery suite to **17 integration tests**.

The actual Docker `maintenance-smoke` also passed: pause/drain, API stop, restart
while paused, maintenance HTTP response, explicit resume and normal deployment
smoke. The existing backend regression suite passed all 288 tests after this change.
The isolated deployment retained two users/two households; personal data was not used.

## Approved household-only private-data rehearsal

A subsequent approved rehearsal selects one existing household and its current
members, excluding unrelated test households/users. The original database opens
read-only; the full consistent source backup is held in RAM and only selected rows
are persisted. All artifacts/configuration remain outside Git and Docker build
contexts in a private host directory; the rehearsal uses an independent Docker
project, network, PostgreSQL volume and household storage. It does not replace the
original installation or modify the existing synthetic test group.

The [household-scoped operator workflow](../deploy/operations/README.md#household-scoped-private-rehearsal)
compares account balances, budgets, actuals, forecast, cash outlook, funds and
reconciliation at a fixed clock. The private-data rehearsal matched all **26 output
groups** before and after an age-encrypted full backup/restore into a fresh database
and root. Password hashes were preserved; historical sessions/codes were invalidated
only in the copies. The recovered copy retains maintenance. No names, financial
amounts, credentials, private artifact paths or personal count reports belong in Git.

The synthetic suite now includes household extraction and comparison coverage:
**18 real-provider integration tests**, plus the five encryption packaging tests.
The private review deployment disables scheduled scans and outgoing recovery email,
uses fresh installation keys, and binds only to loopback. User review is still
required before a final fresh-snapshot cutover; the original remains authoritative.
