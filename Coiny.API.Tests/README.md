# Coiny API Tests

From the repository root, with .NET 10 installed:

```sh
dotnet restore Coiny.slnx
dotnet test Coiny.API.Tests/Coiny.API.Tests.csproj
```

The regression tests use EF Core's SQLite provider with isolated in-memory
connections. Hybrid integration tests use disposable real PostgreSQL databases and
temporary household SQLite files, and skip unless test configuration is supplied. They do not use or reset `Coiny.API/coiny.db`, require an externally running API
server or configured JWT signing key, or change the development user's data.

Coverage includes:

- Account security with real Identity and JWT middleware: server logout, session
  ownership/expiry, password-change/reset revocation, single-use/expired reset
  links, legacy-token rejection, old-identity lockout, and IP rate limits. Recovery-code coverage includes hash-only storage, password-gated generation/revocation, replacement, user isolation, no-SMTP recovery, lockout clearing, complete invalidation, concurrent redemption, and rollback on storage failure. Local
  loopback SMTP capture verifies configured reset-link encoding and transport
  configuration; no real emails are sent.

- Batch-import HTTP contracts and authentication; required categories/directions;
  duplicate decisions within files and against the ledger; commit-time revalidation;
  idempotent/concurrent retries; 2,000-row batches; daily and later snapshot balances;
  atomic rollback and household-scoped import history.

- Atomic manual transfers, liability payments, unlinked/external entries, different
  posting dates, reporting exclusion, link/unlink without financial changes,
  incompatible/foreign pairs, stale-link concurrency, backdated snapshots, and
  rollback of both entries and snapshots on storage failure. Reconciliation coverage includes
  candidate filtering/ranking, reviewed versions, one-sided counterpart creation, rollback,
  and linking two separately imported Bills/Spending rows without changing balances,
  snapshots, transaction count, import provenance, or idempotent batch retries.

- Household-scoped member rosters, owner-only removal, protected owners, preserved
  login/financial data, access after removal, and transactional invitation revocation.

- Owner-only invitation management, code hashing/normalization, 10-minute expiry,
  rotation/revocation, preview, atomic redemption, concurrent join/create races,
  membership rollback, and HTTP authorization/rate limiting.

- Published income periods across months/revisions, household vs individual paydays,
  same-day boundaries, ended schedules, stable source keys, and identity backfill.

- Paycheck detail persistence/cloning and informational totals, six-month/yearly
  occurrences, leap-year anchors, end bounds, and payment-month allocations.
- Anchored weekly/every-two-weeks schedules and three-paycheck months.
- Twice-monthly/monthly schedules, short months, leap years, inclusive date
  bounds, maximum calendar dates, and invalid definitions.
- Resumable shared drafts, household isolation, publication validation,
  expected edit versions, and rollback after a simulated storage failure.
- Published revision selection, preserved history, scheduled future revisions,
  unassigned income, over-budget plans, and calendar-event projections.
- HTTP save/resume/preview/publication, required JSON fields and query parameters,
  authentication, household isolation, and expected error status codes. Those
  budget test hosts use test authentication; the separate account-security suite
  exercises real Identity/JWT behavior.
- Setup status for unpublished, current, and future plans; previews without publication.
- Migration apply/rollback/reapply with ledger data and existing budget
  definitions preserved.

The latest non-hybrid regression run passed **290 tests**, including two crypto/CSV
unit checks. The separate focused run passed **21 PostgreSQL/API integration tests
plus those two unit checks** (23 total). Five operator packaging tests run during
integration-image builds. The milestone counts below are historical subsets, not
the current full-suite baseline. Build output and TestResults are ignored
inside this project. Frontend tests run separately from `Coiny.Web`.

See [budget rules](../docs/09-household-budgeting.md) for behavior and the
[setup guide](../docs/06-development-setup.md#verification) for repository checks.

Bill payment tests cover per-occurrence status, optional expense links, household isolation, duplicate-link rejection, ledger preservation, stable allocation identity, HTTP binding/authorization, and migration backfill.

Transaction management tests cover moving entries across dates/accounts, category corrections, notes/provenance, stale edits/deletes, household ownership, linked transfer updates/deletion, storage-failure rollback, bill status retention, and HTTP contracts.

CSV bank balance tests cover dated ledger comparisons, later-history exclusion, mismatch acknowledgment, concurrent balance changes, immutable opening balances, idempotent retries, and unsupported/invalid references.

Mortgage tests cover loan classification/defaults, per-user preference isolation, matching summary/chart calculations, zero-balance mortgages, migration preservation/defaults, and authenticated preference/subtype endpoints.

Multi-account session tests cover atomic file-and-link commits, rollback when a later account fails, matching against existing entries, independent category/duplicate/bank checks, one-to-one link constraints, household isolation, stale-match rejection, idempotent receipts, and authenticated HTTP contracts.

Account reconciliation tests cover whole-date closing balances, liability signs,
explicit review, stale fingerprints, household isolation, idempotent checkpoints,
backdated additions/edits/deletions, opening balance corrections, persistent review
history, atomic rollback, transfer linking versus amount changes, and HTTP contracts.

Transfer creation tests also cover pre-save candidate lookup, explicit new-pair
decisions, reusing imported incoming/outgoing entries, preserved original balances
and metadata, stale/repeated saves, and rollback of the missing entry and link.

Scheduled contribution tests cover manual payment status, expense/transfer links,
one-occurrence-per-transfer-pair protection, conflicting assignments before linking,
editable linked transfers, deletion retaining paid status, month/income-period
projections and remaining totals, household isolation, and HTTP contracts.

Payment tests distinguish unpaid, paid-awaiting-transaction, and paid-linked
states, verify awaiting counts/planned totals stay separate from unpaid totals,
and cover transitions when links or linked ledger entries are removed.

Budget actuals tests cover posting-month attribution, effective category rules,
individual override/reset, payment priority, pending estimates, future months,
single-count transfer contributions, invalid/foreign assignments, version conflicts,
and HTTP access/binding contracts.

`BudgetAvailableCashTests` verifies the account-based outlook, card liability signs, no double counting of posted expenses/income, savings selection, future-period continuity, and household account eligibility. The HTTP suite covers the authenticated calculation route.

`ImportUndoTests` verifies rollback, idempotent retries, stale-preview rejection, imported versus pre-existing transfer handling, old/edited row acknowledgments, payment/assignment cleanup, checkpoint preservation/invalidation, and balance history recalculation. HTTP import tests also exercise authenticated review and undo.

SavingsGoalTests and SavingsFundsTests verify goal calculations, account-purpose allocations, version/ownership checks, draft and published budget integration, corrections, and funded-spending actuals.

## Hybrid PostgreSQL/SQLite integration

Run `python3 deploy/docker-test/test-stack.py integration` from the repository root.
This executes `HybridStorageIntegrationTests` in Docker against real PostgreSQL
with generated disposable databases and the actual split-context migrations.
These checks are explicitly skipped in ordinary runs without `COINY_TEST_POSTGRES`;
the Docker command supplies it privately. The tests reject non-test database
configuration and never use an existing household financial database.

`HybridRecoveryIntegrationTests` extends that profile with rich financial output
comparisons across three interrupted migrations, malformed-source rejection,
financial-only recovery preserving central revocation, and actual PostgreSQL
17 dump/restore into fresh database/storage. See the [rehearsal evidence](../docs/22-migration-recovery-rehearsal.md)
for scope and production limitations.

Encrypted recovery additionally uses `age` and Python 3 in the integration image.
Five actual-encryption operator tests run during its build. The thirteen PostgreSQL
integration tests include general offline migration planning/apply and full recovery
exclusively from a decrypted, validated backup bundle. See
[operator commands](../deploy/operations/README.md) for limits and key custody.

Maintenance expands the real-provider integration suite to 17 tests. It covers
cross-instance admission/draining, durable cancellation/restart behavior, explicit
matching-operation reopening, missing-state rejection, HTTP central/financial routes,
and encrypted recovery that remains paused until security checks finish. The local
`maintenance-smoke` action additionally stops/restarts the actual test API while
paused, then explicitly reopens it and runs normal deployment smoke checks.

The household-scoped extraction rehearsal adds an eighteenth integration test. It
selects only one synthetic household/member set, proves unrelated data is absent,
keeps the source digest unchanged, migrates the selected snapshot, and compares
account/budget/forecast/cash/fund/reconciliation output groups at a fixed date.

Household data tools add two crypto/CSV unit tests and three real-provider HTTP
tests for restore safety, portable imports across installation/household IDs, owner
permissions and unsafe schema rejection. See [household backups](../docs/23-household-backup-restore-export.md).
