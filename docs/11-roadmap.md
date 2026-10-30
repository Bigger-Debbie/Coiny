# Roadmap and release readiness

[Documentation index](../README.md#documentation)

## Agreed v1 feature scope: implemented

Core financial workflows, published budget management, forecasts, savings funds,
account security, and household backup/restore/export are implemented in `main`.
This is the point to finish release preparation rather than add more product scope.

- Accounts, transactions, transfers, category management, snapshots/trends, and
  statement reconciliation with audit-preserving invalidation.
- Multi-account CSV imports, merchant/category rules, saved mappings, correction,
  and safe reviewed undo.
- Monthly/income-period budgets, dated item changes, linked split payments,
  actuals, potential savings, available cash, and forecasts through one year.
- Account-backed savings funds, component goals, existing-cash allocation,
  affordability reviews, contribution schedules, and period skipping.
- Manual/daily recurring detection, household alerts, and personal unread counts.
- Unified settings; password recovery/change, offline recovery codes, revocable
  sessions, login lockout and rate limits.
- Owner-only encrypted household downloads, optional portable backup passwords,
  restore previews/safety copies, and CSV exports in hybrid installations.

Feature behavior is described in the linked guides in the [README](../README.md#documentation).

## Architecture and local verification: implemented

The app supports opt-in PostgreSQL control storage and one SQLite financial file
per household. Default non-container development retains the combined SQLite
context. Migration never happens implicitly when the code is updated.

Real-provider integration, resumable/checksum-bound migrations, cooperative
maintenance/draining, encrypted operator recovery, and household data tools are
implemented. Synthetic rehearsals and an approved household-only private copy
verified financial outputs across migration and recovery. The user also completed
a portable backup restore into a new user/household in an independent recovery lab.

The retained local deployments are the private personal review site (6081) and
recovery lab (6082). The old synthetic `coiny-test` and architecture POC containers
and volumes were removed; source/tooling remains available to recreate synthetic
tests. Personal review/recovery stores must not be treated as disposable.

See [hybrid delivery](21-hybrid-storage-and-azure.md),
[migration/recovery evidence](22-migration-recovery-rehearsal.md), and the
[operator runbook](../deploy/operations/README.md).

## Remaining release gates

1. Final workflow/mobile review and security/household-isolation release checks.
   Automated browser end-to-end coverage is not yet configured.
2. Production packaging: pinned release images, environment/proxy/HTTPS settings,
   persistent PostgreSQL/SQLite/Caddy/Data Protection storage, migration/upgrade
   instructions, secret custody, and operator account-recovery procedure.
3. Scheduled encrypted off-VM backups, retention and disk monitoring, key recovery,
   cleanup policy for old household files/safety backups, and fresh-host restore drill.
4. Azure VM/data disk/network setup, domain and Caddy/Let's Encrypt, SMTP/reset-link
   validation, reboot/persistence checks, health/backup/certificate alerts, and cost review.
5. Reviewed final stopped-writer snapshot and live cutover, rollback/update runbook,
   then a small tester rollout. The original database remains authoritative until
   this cutover is explicitly completed; do not accept writes to both copies.

The chosen target is one Linux VM running Compose with Caddy/web, API, and
PostgreSQL containers. Hosted and self-hosted installations should share this
storage model. Public production deployment and Azure provisioning are not complete.

## Post-v1 refinements

- Individual account-balance forecasts and explicit paycheck-to-transaction matching.
- Multi-account funding for a single savings fund.
- More tolerant recurring-pattern matching and older import-history pagination/bulk corrections.
- XLS/XLSX import, richer reporting, household leaving/switching and role changes.
- MFA, email verification, and refresh tokens.
- Client-held encryption/shared household keys and zero-knowledge privacy require
  a separate architecture design; separate household databases alone do not provide it.

No additional product feature is required by the agreed v1 scope above.
