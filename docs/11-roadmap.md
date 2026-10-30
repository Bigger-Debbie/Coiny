# Remaining product work

## Recurring transactions and subscriptions

Manual scans, daily opt-in household scanning, persistent alerts, and personal
unread badges are implemented; see [recurring detection](14-recurring-detection.md).
Future improvements include recognizing changing bank reference strings and
patterns with missing cycles while keeping suggestions reviewable.

## Savings funds and component goals

Account-backed savings funds, component targets/recurring costs/reserves, paycheck
contribution schedules, affordability reviews, account makeup, and budget integration
are implemented; see [savings funds](19-savings-funds.md). Multiple-account funding
remains deliberately outside the first version.

## Other planned areas

- Individual account-balance forecasting and explicit paycheck/source matching. The zero-based
  running remainder across complete household income periods is implemented for
  this period and 1/3/6/12 months, carrying surpluses and shortfalls forward.
  Allocation-detail forecasts, comparisons, category defaults, and individual
  assignments are also implemented. The optional available-cash card now uses a
  selected funding pool of cash accounts and credit cards; per-account future flows
  and explicit paycheck matching remain future work.
- Import review/correction and safe undo are implemented, including transfer/payment
  links, reconciliation invalidation, and audit history. Future work can add bulk
  correction tools and older-history pagination.
- Additional budget-maintenance refinements; direct item editing with effective dates is implemented.
- Password recovery/change, offline recovery codes, revocable sessions, and login protection are implemented; see [account security](20-account-security.md). SMTP and production key/proxy configuration remain deployment tasks.
- Owner-only encrypted household downloads, restore preview/safety backups and CSV export are implemented; see [household data tools](23-household-backup-restore-export.md).
- Merchant/category rules and saved CSV column mappings are implemented; safe import undo/correction is also implemented.

Statement checkpoints and account reconciliation are implemented; see
[account reconciliation](12-account-reconciliation.md).

## Agreed V1 completion scope

Savings funds, account security, and household backup/restore/export are implemented
and tested in the isolated hybrid deployment. Recovery email needs SMTP
configuration and deployment validation. Other refinements above are post-V1.
After these, focus on
infrastructure, container self-hosting (including an explicit server recovery command), and hosted deployment. Per-household databases
and client-side encryption/shared household keys are architecture ideas to evaluate
before hosting; no zero-knowledge privacy claim is currently implemented.

## Hybrid storage and hosted delivery

The synthetic PostgreSQL/per-household SQLite experiment is complete. Real-app
integration, migration verification, recovery, Caddy/Compose packaging and Azure
cutover follow the [delivery plan](21-hybrid-storage-and-azure.md). The main app
currently still uses its original single SQLite context.

The experimental split runtime is now enabled in the isolated `coiny-test` Docker
group, with its existing test users/households migrated. Default local development
and the original real database remain on the legacy model. Real-data migration,
user-facing backup/restore/export and production deployment gates remain outstanding.

A [synthetic migration and database recovery rehearsal](22-migration-recovery-rehearsal.md)
now passes against the real models. [Offline operator tooling](../deploy/operations/README.md)
adds encrypted snapshot packaging, verified recovery extraction, and general
read-only migration plans/checksum-bound offline apply. Durable maintenance admission/drain is implemented with a required API/job stop
before snapshots. Production packaging, secret recovery, scheduling/off-VM
retention, export and user-facing backup/restore remain planned.

An approved household-only personal-data rehearsal has matched financial outputs
before and after encrypted recovery in separate local stores. The original remains
authoritative; user review and a fresh stopped-writer snapshot precede live cutover.
Private data/artifacts are excluded from Git and image build contexts.

The user-facing household data tools now provide both installation-linked and portable
password-protected encrypted downloads. Production release/hosting, scheduled offsite
backups, key custody/retention and reviewed live cutover remain infrastructure work.
