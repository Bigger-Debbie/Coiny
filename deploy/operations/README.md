# Offline operator tools

These tools are an infrastructure rehearsal, not the household backup/export UI.
Run them on Linux or macOS with trusted local files. Do not run on the personal
installation until a private rehearsal is approved. No cloud resources are needed.

## Inspect and migrate a legacy snapshot

Stop the source API and workers and use SQLite's backup API to create a separate,
closed snapshot. Do not pass a live database file or copy it while ignoring WAL.
Protect the snapshot directory with mode 0700 and the snapshot with mode 0600.

The API executable recognizes these commands before registering the web app:

```sh
dotnet Coiny.API.dll --migration-plan /private/rehearsal/source.sqlite /private/rehearsal/plan.json
```

This opens SQLite read-only, checks migration history, integrity, foreign keys and
cross-household references, then writes a new private report with table counts and
source SHA-256. It does not open PostgreSQL or household destination storage. The
report contains no names, emails, transaction descriptions, balances or credentials.
Existing reports are never overwritten. Keep even count reports private.

After reviewing the report, configure `HybridStorage__ControlConnection`,
`HybridStorage__DataRoot` (absolute path), and `HybridStorage__Node` through protected
configuration/environment. Never put passwords in command arguments. Stop every
API/job that could use the destination; `--offline` is an operator assertion, not a
global PostgreSQL maintenance lock. The household root lock rejects another local
owner but cannot stop a different installation from writing to PostgreSQL.

```sh
dotnet Coiny.API.dll --migrate-snapshot /private/rehearsal/source.sqlite REVIEWED_SHA256 --offline
```

The command checks the reviewed digest before opening the destination, applies
control migrations, then runs the shared verified/resumable migration engine. The
control destination must be empty or contain this snapshot's interrupted journal.
Retry with exactly the same snapshot. The command never starts an HTTP listener or
worker. It preserves the source and requires users to sign in again. Completed
imports preserve subsequent split-store writes. Deployment activation must refuse
an unfinished journal, as the Docker test API already does. Do not restart the
original writable installation after cutover; it would create divergent histories.

## Encrypt an already coordinated snapshot set

Install `age` and Python 3 in an operator image. The test image uses Ubuntu packages;
the application runtime does not gain these tools. Generate an offline recovery key:

```sh
umask 077
age-keygen -o /secure/offline/coiny-recovery-key.txt
age-keygen -y /secure/offline/coiny-recovery-key.txt
```

Keep the private identity in a password manager/offline recovery location separate
from the VM and backups. Test recovery before relying on it. The backup host needs
only the printed public `age1...` recipient. Losing every private recovery key makes
these packages unrecoverable. The version label is an operator inventory reference,
not a secret. Key rotation uses a new recipient/version for future packages; keep
older recovery keys until their retained packages expire. Production key custody,
rotation scheduling and disaster recovery secret management are still outstanding.

Supply a private, flat directory of **closed, coordinated snapshots** containing:

- `control.dump` made with PostgreSQL 17 `pg_dump --format=custom`.
- Every required household SQLite backup and its existing identity/schema manifest.
- Optional release/configuration recovery notes that contain no private key.

Stop API and jobs across the whole installation for the complete snapshot window.
The packaging tool does not create database snapshots or establish consistency.
A partial snapshot set cannot provide complete installation recovery.

```sh
python3 backup_bundle.py pack --snapshots /private/snapshots --output /private/coiny-backup.age --recipient PUBLIC_AGE_RECIPIENT --release RELEASE_COMMIT --key-id recovery-v1
python3 backup_bundle.py unpack --package /private/coiny-backup.age --destination /private/new-recovery --identity /secure/offline/coiny-recovery-key.txt
```

The encrypted manifest binds each file's name, length and SHA-256 to the release,
recipient and recovery-key label. Household manifests carry household/schema
versions; the central dump carries migration history. Installation secrets are
explicitly marked **not included**: restore or rotate JWT/Data Protection/SMTP and
other configuration through a separate protected recovery runbook. Never include
the recovery private key in its own package.

`age` provides authenticated encryption, not proof of the author's identity:
anyone knowing the public recipient can create a different valid encrypted file.
Accept packages only from controlled backup storage and an operator-reviewed
inventory. Restore input, especially PostgreSQL archives, must remain trusted.
See the [upstream age documentation](https://github.com/FiloSottile/age).

Publication refuses overwriting an existing package. Encryption writes to private
staging and publishes a completed ciphertext by same-filesystem hard link.
Recovery authenticates the entire ciphertext before inspecting/extracting tar
members, rejects links/traversal/duplicates/unlisted or mismatched files, and
requires a new destination. Only fully verified files reach that destination;
it may briefly be empty/partially populated during publication, so do not consume
it until the command succeeds. A normal failure removes staging. A killed process
or power loss can leave private staging plaintext: use encrypted host disks and
an operational cleanup policy, not a claim of secure deletion.

Limits: 10,000 files, 16 GiB payload, one hour per age process; requires enough
private local disk for the plaintext archive plus encrypted/decrypted staging.
The child process file-size limit bounds ciphertext/plaintext disk output. No
compression or archive-provided permissions are honored. This tool does not
activate recovered databases or overwrite active household files.

After unpacking, use the tested schema/identity-aware household restore and
PostgreSQL recovery procedure. Revoke historical sessions/recovery codes, rotate
security stamps and review restored memberships before reopening a full restore.
These steps, off-VM retention, scheduled backups, maintenance fencing and the Azure
recovery drill remain deployment work.

## Verify

`python3 -B -m unittest discover -s deploy/operations -v` runs five actual-age tests.
`python3 deploy/docker-test/test-stack.py integration` also exercises a real
PostgreSQL dump + household snapshots through encryption/decryption before recovery,
and compares all fixed-clock financial outputs. It uses temporary synthetic stores.

## Maintenance admission, draining and stop-before-backup

Hybrid installations now use a central `InstallationMaintenance` singleton and a
stable PostgreSQL shared/exclusive advisory-lock key. Every HTTP request enters
before authentication and holds a shared session lock until its handler completes.
The recurring scan worker does the same for its complete pass, including provisioning
and preference recovery. Startup provisioning also requires admission. The mail
queue does not write databases and is stopped with the API before backup.

The operator command first persists a pause operation UUID, which rejects new work,
then waits up to 60 seconds for an exclusive lock. This drains previously admitted
readers and writers across participating processes. Lock sessions disable pooling;
closing a lease actually closes its PostgreSQL session. During a pause HTTP returns
503, a plain maintenance message, and `Retry-After: 60`, including sign-in and central
household/security routes. Legacy SQLite-only mode is unchanged.

Use a new UUID and save it in the protected operation record. Configure the same
`HybridStorage__ControlConnection`, node and root settings as the installation.
These commands do not start an HTTP listener or worker and need no public endpoint:

```sh
dotnet Coiny.API.dll --maintenance status
dotnet Coiny.API.dll --maintenance pause OPERATION_UUID
```

**A successful drain is not permission to back up a still-running installation.**
Stop every API/job process after drain and verify it is stopped. PostgreSQL advisory
locks are cooperative and session-based: connection loss releases them even if
another connection or SQLite task is still executing. Stopping writers is the
required fence for this initial single-VM deployment. This is not partition-safe
online backup, distributed ownership or automated failover. Direct SQL tools,
offline migration tools and future jobs must be included in the stop/runbook.

Once processes are stopped, keep the durable pause in place throughout coordinated
snapshots, packaging and verification. A command failure, cancelled drain, crash,
API restart or operator lease closing **does not automatically resume admission**.
A restarted API serves maintenance and does not run startup provisioning or scans.
A drain timeout is a failed drain; do not proceed to snapshot on its strength.
Inspect and stop the remaining writers. An interrupted pause may be retried with
its same UUID. A different UUID cannot replace it or resume it.

After the approved backup/maintenance work and checks finish:

```sh
dotnet Coiny.API.dll --maintenance resume OPERATION_UUID
```

Resume requires the matching UUID and an exclusive lock, so it cannot reopen while
a drain holder or admitted operation remains. It clears the durable flag; start
API/jobs and verify normal behavior. An already-running paused scan worker retries
on its regular timer (within 15 minutes); starting it after resume runs immediately.
The command is operator access control by host/configuration custody, not a user
password or a public API authorization feature. Do not edit the row manually to
bypass a failed safety check.

Apply the new control migration with API/jobs stopped before deploying this version.
Missing schema/state or an unavailable central database refuses admission. The
Docker test `migrate-hybrid` command performs this controlled upgrade without
reimporting an already completed source or resetting test users.

A full central backup taken while paused restores the pause UUID too. Complete the
recovery security/membership checks, stop/fence the old installation, then explicitly
resume the recovered installation with that operation UUID. Do not automatically
clear maintenance during restore. A household-only restore keeps current central
maintenance and membership state.

Local synthetic convenience commands:

```sh
python3 deploy/docker-test/test-stack.py maintenance-status
python3 deploy/docker-test/test-stack.py maintenance-pause
python3 deploy/docker-test/test-stack.py maintenance-resume
python3 deploy/docker-test/test-stack.py maintenance-smoke
```

`maintenance-pause` drains and stops the test API. It retains a private, ignored
`.maintenance-operation` file for retry/recovery. `maintenance-resume` explicitly
reopens and starts the API, then runs normal smoke checks. `maintenance-smoke`
performs pause/stop, starts the API while still paused, verifies the maintenance
response, then resumes and smoke-tests. A failed check retains the operation file;
inspect `maintenance-status` before retrying or reopening. Do not discard the marker
or invent another UUID to evade the durable pause.

Reference: [PostgreSQL advisory locks](https://www.postgresql.org/docs/17/explicit-locking.html#ADVISORY-LOCKS).

## Household-scoped private rehearsal

`household_snapshot.py --source ABSOLUTE_LEGACY_DB --email SELECTED_EMAIL --output ABSOLUTE_NEW_SNAPSHOT`
selects exactly one user/household, retaining the household's current members and
associated rows. It uses SQLite's backup API into memory before extracting, so
unrelated household/user rows are never persisted into the selected snapshot.
The source opens read-only. The schema inventory is explicit; unknown tables,
ambiguous user/household selection, unexpected views/triggers, external foreign-key
references or existing output refuse extraction. Database values are copied with
SQLite `INSERT ... SELECT`, preserving their storage representations, IDs, indexes
and deleted-ID high-water marks. Only role/claim associations of retained members
are copied. Migration history is preserved, migration lock state is not.

Keep private snapshots, Compose files, environment files, logs, keys and reports
outside all Git repositories and image build contexts, in a mode-0700 directory.
Use a separate Compose project/network/volumes and loopback-only review port.
Never reuse `coiny-test` storage or the real installation's data volume. Synthetic
selection/migration tests must pass before using this tool on an approved real copy.

Additional offline API executable commands:

```sh
dotnet Coiny.API.dll --verify-household ABSOLUTE_SELECTED_SNAPSHOT YYYY-MM-DD ABSOLUTE_NEW_REPORT
dotnet Coiny.API.dll --household-backups ABSOLUTE_SNAPSHOT_DIRECTORY
dotnet Coiny.API.dll --restore-household-backups ABSOLUTE_VERIFIED_ARCHIVE_DIRECTORY
dotnet Coiny.API.dll --recovery-security
```

Configure `HybridStorage` for the isolated destination and stop its API/jobs first.
The verifier requires exactly one household in both source and destination. It
compares account balances, three monthly plans and income-period actuals where a
published period exists, three-month forecast, default available cash, savings funds,
and every account's reconciliation history. The current published period is
required; a month before the budget exists is explicitly represented as absent.
The report contains only component digests, match flags, the source digest and fixed
date. Decimal scale is canonicalized; timestamps compare at PostgreSQL microsecond
precision. The report is private and refuses overwrite. A failed comparison must
not activate the destination.

Household backup/restore commands wrap the already-tested identity/schema-aware
helpers. Restore requires a matching complete household manifest inventory and
empty destination storage. It never starts the web server or changes memberships.
The recovery-security command invalidates historical sessions/recovery codes and
rotates user security stamps while preserving password hashes. It applies to the
configured **copy**, not the original installation. Back up/decrypt/restore through
fresh PostgreSQL storage and a fresh household root, verify all outputs again, and
keep that recovered copy paused until its explicit activation review.

`Operations__ScheduledJobsEnabled=false` disables recurring scan scheduling for a
review-only deployment; normal deployments default to enabled. Also leave recovery
email unconfigured and use fresh JWT/Data Protection material, so the isolated copy
cannot send mail or reuse original installation sessions. A review site is not a
live cutover: changes made there do not synchronize back to the original. A later
cutover must stop the original writer and take a fresh final snapshot.

## Scope and retained local environments

These commands operate on installation/control snapshots and reviewed household
copies. They are distinct from the owner's Settings backup/upload tools; see
[household recovery](../../docs/23-household-backup-restore-export.md). The
`--recovery-security` operator command invalidates historical credentials in a
recovered copy; it does not issue a login reset link. A server account-reset command
remains release work.

The private review site (6081) and the recovery lab (6082) are retained. A portable
restore was manually completed in the latter; preserve its volumes as private data.
Old synthetic/POC groups were retired, while their tooling remains reproducible.
See the [release roadmap](../../docs/11-roadmap.md) for production backup scheduling,
key custody, disk retention/cleanup, Azure setup, and live-cutover gates.
