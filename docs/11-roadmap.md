# Release checklist

The v1 feature scope is complete. Remaining work is deployment and release testing.

## Before inviting testers

- Finish the mobile/workflow review and security/household-isolation checks.
- Validate the [self-hosting setup](../Docker/README.md) on the target host, including
  HTTPS, SMTP, restarts, persistence, and update/rollback instructions.
- Configure encrypted offsite backups, retention, disk alerts, and key recovery.
  Test restoration on a fresh host. Old household files and safety copies still
  need an operator cleanup policy.
- Provision Azure, DNS, monitoring, and cost alerts.
- Take a final stopped-writer snapshot, migrate, verify, and complete live cutover.
  Keep the old installation offline once the new one accepts writes.

A server-side account-reset command and automated browser end-to-end tests are
still missing. Public registration currently has no invitation-only switch.

## Later

- Per-account balance forecasts and explicit paycheck matching.
- Funding one savings goal across several accounts.
- More flexible recurring detection, bulk corrections, older import-history pages,
  and XLS/XLSX imports.
- Household switching/role changes, MFA, email verification, and refresh tokens.
- Client-held encryption keys require a separate design; household files alone
  do not provide zero-knowledge privacy.
