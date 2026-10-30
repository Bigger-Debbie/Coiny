# Hybrid storage and Azure delivery plan

Status: experimental split runtime enabled and migrated in the local Docker test group; production migration and hosting pending. Updated 2026-10-29.
Branch: `feature/hybrid-hosting`, based on `prototype/hybrid-household-storage`.
The primary checkout and original Coiny database remain unchanged.

## Target and constraints

One Azure Linux VM runs Docker Compose: Caddy, Coiny, and PostgreSQL. Caddy serves
Angular static assets and proxies `/api` to ASP.NET Core on a private network.
PostgreSQL owns identity and orchestration; each household has a separate SQLite
financial database. Hosted and self-hosted installations use the same images and
storage model. Start with one application instance and one owning node, not HA.

Initial planning budget: $100–125/month, targeting normal spend below $100.
Candidate: East US Ubuntu LTS, 2 vCPU/8 GiB burstable VM, Standard SSD OS and data
disks; recheck availability/prices before provisioning. Container builds run outside
the VM. No Azure resources, billing commitments, DNS changes, or live data cutover
have been authorized by writing this plan; prepare these for review first.

Use an ext4 filesystem on a persistent managed data disk for PostgreSQL, household
files, and Caddy state. Never use temporary VM storage or Azure Files for SQLite
WAL. Maintain separate off-VM encrypted backups. Disk replication is not a backup.

## Delivery phases and completion gates

| Phase | Deliverable | Gate before proceeding |
| --- | --- | --- |
| 0 — Architecture experiment | Isolated synthetic-data lab | Complete: ten checks, evidence under `experiments/hybrid-db/` |
| 1 — Model boundary | Explicit entity inventory, staged control/financial contexts, migration contract | Boundary tests cover every legacy entity, table/column preservation and local relationships; runtime still uses legacy context |
| 2 — Real application integration | PostgreSQL control context; household SQLite context/resolver; durable lifecycle operations | Real registration, login, household creation and financial routes work; unauthorized requests open no files |
| 3 — Migration rehearsal | Offline source-to-new-stores command, manifest and verifier | Copies migrate twice reproducibly; balances, history, links and security checks match |
| 4 — Regression and operations | Multi-household feature tests, backup/restore/export, failure recovery | App tests pass; full recovery on fresh volumes demonstrated |
| 5 — Container release | Caddy/app/PostgreSQL Compose, immutable images, self-host guide | Clean install, update, reboot, proxy/security and restore smoke tests pass |
| 6 — Azure rehearsal | Infrastructure definition, test hostname, monitoring and runbooks | Approved resources deployed; HTTPS, SMTP, backup/restore and resource usage verified |
| 7 — Production cutover | Final verified migration, DNS switch and release record | Owner reviews rehearsal evidence; explicit live cutover approval |
| 8 — Stabilization | Closely monitored tester release and operational handoff | Successful backup/restore drill and agreed observation period |

Implement and verify each phase in reviewable increments. Do not merge an incomplete
storage cutover into main or silently switch the primary checkout's connection.

## Phase 1: database ownership

| Store | Entities/data |
| --- | --- |
| PostgreSQL | ASP.NET Identity tables, `ApplicationUser` (including personal net-worth preference), `AuthSession`, `AuthRecoveryCode`, `Household`, `HouseholdMember`, `HouseholdInvitation` |
| PostgreSQL, new orchestration | Household storage registry and schema/status/owner, provisioning and maintenance operation records, backup manifests, migration runs and job scheduling metadata |
| Household SQLite | Institutions, accounts and account subtype entities; categories; transactions and transfers; snapshots and reconciliations; budgets/revisions/income/allocations/schedules; bill payments and all linked transactions; assignments and budget category rules; savings funds/components/activity/schedules; imports/sessions/files/baselines/undo history; saved CSV mappings; merchant rules |
| Household SQLite | `HouseholdRecurringSetting`, `RecurringExpenseReview`, `HouseholdAlert`, `HouseholdAlertRead` |

Inventory the actual EF model, including types discovered through navigation
properties and owned entities, not just the context's DbSet properties. Add a
coverage check so future entities cannot accidentally enter both stores.

Introduce `ControlContext : IdentityDbContext<ApplicationUser>` with explicit
control configurations and `HouseholdContext : DbContext` with explicit financial
configurations. Do not use unfiltered `ApplyConfigurationsFromAssembly` in both:
EF can discover a foreign store through a navigation and silently create tables.
Keep the old context/migration chain available to the migration reader until the
conversion is verified; production uses separate provider-specific migration sets.

Remove cross-store EF navigations and foreign keys. Keep stable household/user IDs
as scalar references in financial/audit records, and validate access centrally.
Preserve existing integer household/entity IDs and string Identity IDs; storage
keys are newly generated opaque UUIDs, never user-provided paths. Household files
have a dedicated metadata row containing household ID, schema version and instance
ID; retain HouseholdId financial columns as defense in depth, not duplicated
central membership records. Local relationships and delete protections stay intact.

Keep alerts and their read state together in SQLite to preserve transactionality
with scan results and avoid copying merchant/amount details into the control DB.
Alert read UserId becomes a scalar reference; revoked users cannot access the
household, and restored read markers never restore membership. Central job metadata
contains no financial alert payload. A scheduler enumerates registered households
and opens a household scope to check its recurring setting; it no longer queries
all financial settings through one context. Migration/restore and jobs share the
same household maintenance gate.

Preserve CoinyContext's reconciliation-invalidation SaveChanges behavior in the
financial context. Keep ledger/snapshot/import/payment/fund changes in their
existing single-household transactions. Audit actor IDs remain historical scalar
references even when an identity is later removed.

## Phase 2: routing, security and lifecycle

Authorize session and live membership in PostgreSQL before resolving/opening the
household file. Resolve only a registered Ready household owned by this node, then
verify file metadata. Fail closed on catalog outage, missing file, wrong identity,
unknown schema, wrong owner or maintenance state. Never auto-create an empty file
when a registered household database is missing. Apply this to jobs and admin
commands too, using explicit trusted scopes rather than fake HTTP users.

Use per-household writer coordination, bounded busy timeouts, WAL and foreign keys;
keep independent readers. Provision/upgrade/restore use durable intent, idempotent
steps and restart recovery, since PostgreSQL and SQLite cannot commit atomically.
Serialize lifecycle operations and drain active requests before maintenance. Fail
startup for a second application instance sharing these files; the single-process
lab semaphore is not distributed ownership or production fencing.

Registration creates central user/membership plus a recoverable provisioning job;
financial requests wait for Ready. Password changes, recovery, session revocation,
owner removal and invitations remain central transactions. Review uniqueness,
case normalization, UTC timestamp handling and provider-specific constraints.
Persist ASP.NET Data Protection keys separately from JWT signing material, protect
both, and test restart recovery of tokens/keys. Never log passwords, tokens, reset
links, SQL parameters or financial request bodies.

## Phase 3: migration and verification contract

Build a CLI with dry-run, execute and verify modes. Default to dry-run; require an
explicit source snapshot path and empty destination namespace. Refuse source and
destination overlap, incompatible schemas, existing destinations without matching
run manifests, and inconsistent cross-household relationships. Never seed/reset.

Obtain a consistent source through SQLite's backup API; never copy only a live .db
while ignoring WAL. Opening the live source and making a real-data copy are a
separate deliberate operation. First test with synthetic databases, then a private
copy of the real database. Copies and reports containing private data stay ignored
and outside image build contexts.

Preserve IDs, password hashes/security stamps, dates, exact decimal financial
values, category rules, transfer pairs, payment multi-links, skipped contributions,
fund history, imports/undo baselines and reconciliation status. Do not recalculate
or repair history as part of migration. Restore PostgreSQL sequences after explicit
ID inserts. Reissue sessions at live cutover and require sign-in; old browser JWTs
must not remain valid. Document effects on outstanding recovery links/codes before
cutover and test the chosen invalidation policy.

The verifier records counts and canonical per-table digests, per-account balances,
foreign-key/integrity checks, orphan checks and relationship coverage, plus selected
budget actual/forecast/fund outputs at a fixed clock. Compare secret-bearing tables
without printing values. Manifest states distinguish staged, verified and activated;
interrupted runs resume safely or can be discarded without touching the source.

## Phase 4: recovery and functional gates

Run existing backend/frontend checks plus actual PostgreSQL-backed security tests
and SQLite financial tests. Cover two households, shared members, removal during
an active session, wrong-file routing, catalog outage, concurrent imports, duplicate
submission, migration/restore interruption and restart recovery.

Exercise forecast actuals, available cash, income effective dates, multi-linked
payments/unlinking, import undo and reconciliation invalidation, funds/components,
skipped periods, merchant rules/mappings and automatic recurring alerts. Compare
behavior against the original app using deterministic fixtures.

Implement household financial backup/restore/export and full installation disaster
recovery. Household restore must not restore central users, membership or sessions.
Full recovery requires compatible PostgreSQL backup, household files, manifests and
protected operational keys; request maintenance, drain admitted work, then stop
all API/job processes for a consistent full backup. Keep maintenance set through
verification and explicitly resume afterward. Use SQLite backup API and supported PostgreSQL backup tools. Test checksums,
wrong-household refusal, version compatibility, interruption and fresh-volume
recovery. Proposed starting policy: nightly off-VM backups, 7 daily + 4 weekly,
24-hour recovery-point target and 4-hour recovery-time target, to confirm in a drill.

## Phase 5: production packaging

Publish pinned, versioned Linux amd64 images and ARM64 for Apple Silicon/self-host
verification. Caddy serves the Angular SPA and routes `/api/*` without breaking
existing controller paths; unknown API paths must not return the SPA. Only Caddy
publishes 80/443. API and PostgreSQL have no public host ports. Containers run with
minimal privileges and explicit persistent paths; Caddy cannot mount household files.

Configure trusted forwarded headers from Caddy only; verify HTTPS redirects, secure
cookies if used, client IP rate limits, allowed hosts/origins and password-reset
public URL. Persist Caddy certificates and Data Protection keys. Add liveness versus
readiness checks, graceful shutdown, controlled migrations, resource limits and
bounded log retention. Provide secret templates without real keys; preserve keys on
updates. Configure SMTP and offline administrative recovery for self-hosters.

Build release artifacts in CI; include dependency/image scanning, health checks and
release verification. Supply install/update/backup/restore/recovery instructions.
Avoid development seed endpoints and lab endpoints in production images. Resolve
tracked build artifacts as separate repository maintenance, never ship them as the
container build input.

## Phases 6–8: Azure rehearsal and live migration

Prepare Bicep (or equivalent) for a dedicated resource group, VM, static public IP,
network/security rules, persistent managed disk, backup storage, managed identity,
and budget alerts at $75/$100/$125. No public PostgreSQL/API; restrict administrative
access to the chosen private or source-restricted SSH method. Document disk mounts
by UUID and ensure failure to mount prevents app startup onto an empty directory.
Use managed identity for backup storage where practical, least privilege and private
backup containers; encryption keys must be recoverable outside the VM.

Before paid provisioning, review the exact SKU/region quote, resources, administrator
access, domain/test hostname, subscription, SMTP provider and backup policy with the
owner. Budget alerts do not impose a spending cap. Avoid adding Bastion, NAT Gateway,
paid registries or broad log ingestion without evaluating their cost.

Rehearse with synthetic data first, then an approved private copy. Test domain DNS,
Let's Encrypt issuance, persisted certificate renewal configuration, SMTP/reset
links, login lockout, reboots, container recreation and disk persistence. Prove a
restore onto fresh storage. Measure RAM, CPU credits, latency, disk capacity/IO,
backup age, health and actual cost. Alert on failure, disk pressure, backup failure
and certificate issues without collecting sensitive payloads. Single-VM downtime
is an accepted initial limitation; no HA claim.

For live cutover: approve verified rehearsal results and maintenance window; pause
writes/background jobs; take and verify the final snapshot; migrate and verify;
start the release privately; test critical workflows; switch DNS; require sign-in;
observe closely before reopening imports. Keep the original source offline/read-only
and retain the release manifest and pre-cutover backup. Prevent simultaneous writes
to old and new installations.

Rollback before new writes: switch back to the unchanged original system. After new
writes: freeze the new system and preserve its latest backup; do not blindly revert
to the old snapshot and lose accepted transactions. Reconcile/export new writes or
fix forward under a reviewed recovery procedure. Validate recovery targets during
rehearsal, not after a real incident.

## Evidence and references

- [Experiment instructions](../experiments/hybrid-db/README.md) and [results](../experiments/hybrid-db/EVIDENCE.md): mechanics proven on synthetic schemas, not the actual EF split or hosted capacity.
- [SQLite WAL](https://www.sqlite.org/wal.html): same-host shared-memory requirement; no network filesystems.
- [EF migrations per provider](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers).
- [Azure managed disks](https://learn.microsoft.com/en-us/azure/virtual-machines/managed-disks-overview): VM block storage.
- [Caddy automatic HTTPS](https://caddyserver.com/docs/automatic-https): issuance, renewal, ports and persistent state.

Separate household files are isolation, not zero-knowledge encryption. Client-held
keys, MFA, HA and multi-node placement remain separate future decisions.

## Implementation progress

The isolated feature branch now includes `StorageModelOwnership`, `ControlContext`
and `HouseholdContext`. Configurations are explicitly applied per store; the legacy
domain navigations remain available to the original context but are excluded from
the opposite split model. Owned budget schedules stay in financial storage.
Reconciliation invalidation is shared between the legacy and financial contexts.

Boundary tests create a synthetic household schema without central tables and
verify reconciliation invalidation, complete entity ownership, stable table/column
names and preserved local foreign keys. These tests currently use SQLite for model
verification; they do not prove PostgreSQL SQL/provider compatibility. PostgreSQL
provider registration, its migration set, orchestration metadata, session routing
and all service rewiring are the next increment. No runtime switch or migration
of the original database has occurred.

Verification: all 287 backend tests passed (including three new storage boundary
tests). Two pre-existing nullable warnings in HouseholdMember remain. No frontend
changes were made in this increment.

## Local deployment target

Use the [coiny-test Docker group](../deploy/docker-test/README.md) for incremental
UI/API smoke testing on the Mac. It includes Caddy and a staged PostgreSQL service;
the real API still uses its isolated synthetic SQLite database until phase 2 is
complete. This local test packaging does not mark the production packaging phase
complete.

### PostgreSQL and household-routing foundation

The next increment adds Npgsql EF Core 10.0.3, independently scaffolded PostgreSQL
`InitialControl` and SQLite `InitialHousehold` migrations, a central household-file
registry and local file identity metadata. Design-time factories do not start the
app or read user secrets. The foundation has explicit opt-in DI registration; the
normal API still uses the legacy registration.

`HouseholdDatabaseService` checks live central session/security stamp and membership
before opening a registered Ready file. It refuses missing storage (SQLite
ReadWrite mode), another node, unsupported schema or mismatched file identity. A
root lock restricts storage ownership to one .NET process. This is not a
distributed lease or high availability solution. Existing open contexts are not
retroactively revoked; the check applies when a request obtains its context.

Provisioning records durable intent, migrates an unpublished staging file, commits
its identity, checkpoints/closes SQLite, publishes the file and marks it Ready.
Recovery resumes pending entries on the owning node and repeated calls preserve
existing data. Provisioning is serialized by the singleton service per household.
Maintenance/restore draining and writer coordination for financial requests remain
required before runtime cutover.

Five opt-in Docker integration tests passed against actual PostgreSQL and the
real financial schema: provider/Identity migrations, household separation and live
revocation, provisioning recovery, missing/mismatched storage and node ownership,
and a catalog failure. The catalog failure test removes a table in a disposable
test database; it does not stop the shared PostgreSQL container. Tests use new
`coiny_it_*` databases and delete only their own synthetic data.

Next: rewire identity/security/household services to the control context, financial
services to an authorized request context, and background scans to explicit
household scopes. Only then enable the split runtime in the Docker UI and test
registration/invitations/recovery through the actual endpoints. The current UI
continues to use its isolated legacy SQLite demo data.

Regression verification for this increment: all 287 existing backend tests passed
in the Docker test image, in addition to the five real PostgreSQL checks.

### Real application service cutover (Docker only)

The feature branch now uses `IControlContext` and `IFinancialContext` throughout its
services. Default local configuration still binds both to the unchanged legacy
context; explicit `HybridStorage:Enabled` binds Identity/security/household services
to PostgreSQL and financial services to a household context opened by request
middleware. Financial controllers declare the required scope. Missing, revoked or
foreign membership fails before file access. Reconciliation resolves its audit
actor centrally and keeps ledger/checkpoint changes in SQLite.

Household creation commits a provisioning registry entry with membership and then
provisions its file. Background scans enumerate registered Ready households and
open trusted local scopes. They share per-household write gates with HTTP writes.
Single-process ownership is enforced; there is no multi-node/HA support.

The combined net-worth/recurring preferences save has a durable control intent and
financial receipt. It is recoverable across database commits rather than atomic
across engines. Receipt-based retry never increments the recurring version twice;
a stale edit version cancels the intent. The worker resumes pending operations.

The restricted `--migrate-docker-test` CLI migrated the running test environment:
**two users and two households**. It preserved IDs/password hashes, verified control
rows and every household financial table, preserved deleted-ID sequence high-water
marks, revoked old sessions and retained both the original database and consistent
snapshot. The verifier reads decimal columns through SqliteDataReader.GetDecimal:
legacy physical NUMERIC affinity and current TEXT affinity can store equivalent
values differently. A one-cent mismatch is still rejected. Central timestamps are
compared at PostgreSQL's microsecond precision; financial SQLite timestamps remain
unchanged. This restricted command is not the general-purpose real-data migration
CLI promised in phase 3.

The deployment helper preserves hybrid mode across updates and refuses stale
legacy startup via a persistent volume marker. Failed migrations exit cleanly and
leave the API paused. Completed migrations never overwrite subsequent split data.

Verification: eight real PostgreSQL integration checks include actual HTTP
registration/login, provisioning, invitations/removal, household isolation,
preferences, recovery codes and revocation, as well as recovery of a local
preferences commit. Existing backend tests remain a separate regression gate.
Next work is the broader migration/recovery rehearsal and operational release
packaging, not Azure provisioning yet.

Final cutover regression gate: 288 backend tests passed, including the historical
SQLite decimal-affinity migration regression; eight PostgreSQL/API integration
checks passed separately. No frontend source changed during this service cutover.

Post-cutover checks also passed: a repeated migration preserved split data and
recreated the API successfully; smoke checks passed after restart; the volume
refused a legacy-mode startup; PostgreSQL retained two users, two households, two
Ready household registries and one completed import journal. No Azure resources
were created, and the primary development database was never opened or modified.

### Broader synthetic migration and recovery proof

The [migration/recovery rehearsal](22-migration-recovery-rehearsal.md) now verifies
three interruption boundaries, complete financial output comparisons and actual
PostgreSQL dump/restore plus household snapshots into fresh database/storage. All
12 PostgreSQL/API/recovery tests passed. This proves database recovery mechanics;
encrypted packaging, production write quiescence, secret handling, general CLI
and an approved private real-data rehearsal remain outstanding.

### Encrypted backup packages and offline migration commands

The [operator tools](../deploy/operations/README.md) now provide a read-only legacy
snapshot plan/report, explicit checksum-bound offline migration, and `age`-encrypted
snapshot packaging with verified recovery extraction. The recovery rehearsal restores
from the encrypted package rather than its original plaintext snapshots. Five
packaging tests and thirteen real PostgreSQL/API/recovery checks cover this increment.
General export, production write fencing, protected secret recovery, offsite
scheduling/retention and an approved private real-data rehearsal remain outstanding.

### Maintenance admission and drain

Hybrid HTTP, startup provisioning and recurring scans now participate in a durable
installation-wide pause and PostgreSQL advisory-lock drain. Operator-only
status/pause/resume commands retain a failed/interrupted pause and require its
matching UUID to reopen. Full encrypted recovery preserves that pause through
credential invalidation. See the [maintenance runbook](../deploy/operations/README.md#maintenance-admission-draining-and-stop-before-backup).
This is cooperative admission, not network-partition fencing: stopping all API/jobs
after drain remains required before backup/restore. Production packaging, offsite
scheduling/retention, secret recovery and Azure rehearsal remain outstanding.

### Household-scoped personal rehearsal

The approved household-only private rehearsal now uses an isolated local deployment,
with other households/users excluded and the original left read-only/authoritative.
All 26 financial output groups matched before and after encrypted full recovery into
fresh stores. Private artifacts and configuration stay outside Git/build contexts.
See the [rehearsal evidence](22-migration-recovery-rehearsal.md#approved-household-only-private-data-rehearsal).
User review and a new final snapshot with the old writer stopped are still required
before live cutover; this is not synchronization between installations.

### User-facing household recovery

Settings now provides owner-only [household data tools](23-household-backup-restore-export.md):
installation-linked or portable password-protected `.coiny` backups, validated restore
preview, durable encrypted safety backup, fresh-file/atomic-registry restoration and
readable CSV exports. This is distinct from full-installation operator recovery.
Production key custody, old-file retention/disk monitoring, offsite scheduling,
packaging and the reviewed live cutover remain. No personal destructive restore is
performed by the feature validation suite.
