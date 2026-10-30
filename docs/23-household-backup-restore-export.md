# Household backup, restore and CSV export

Settings → Backup & export provides owner-only household data tools in hybrid
installations. Existing users without a household or on legacy storage do not gain
an upload/restore path into the combined database. This is separate from the
operator's full-installation `age` backup/recovery runbook.

## Download choices

Every `.coiny` download is encrypted. Creating/downloading it requires the current
owner's account password, verified through Identity; this password is authorization,
not an encryption key or a stored backup password.

- **Installation-linked backup** (default): ASP.NET Core Data Protection encrypts
  the file. Restore requires this installation's persisted key ring, application
  isolation configuration and the same household ID. Preserve all required keys;
  deleting/revoking them can make old downloads unrecoverable. A replacement server
  with those keys/configuration restored can recover the file.
- **Portable password-protected backup**: the optional toggle asks for a separate
  password and confirmation (12–1,024 characters). AES-256-GCM uses a fresh random
  16-byte salt, 12-byte nonce and 16-byte tag; PBKDF2-HMAC-SHA256 derives its key with
  600,000 iterations. The authenticated header fixes the file version/mode. Derived
  key buffers are cleared after use. This mode uses no installation key, so a
  compatible Coiny installation can unlock it using only the chosen backup password.
  Changing/resetting an account password cannot change an already downloaded file.
  Coiny cannot reset a forgotten backup password.

Version 1 files begin with `COINYB01` and a mode byte. Portable mode adds salt,
nonce, authentication tag and ciphertext; installation mode carries a protected
Data Protection payload. Parameters are fixed by the format version, preventing
untrusted files from selecting an unbounded password-derivation cost. Passwords
are not placed in filenames, URLs, command arguments, reports or preview tickets.
There is no custom cryptographic algorithm or account-password-hash encryption.

The encrypted payload contains the household SQLite snapshot, original household
and storage identity, creation time, schema/format version and SHA-256. It includes
financial data/history, not central users, password hashes, sessions or memberships.
The SQLite backup API captures WAL correctly under the household writer gate.
Current limits are a 32 MiB SQLite payload and a 64 MiB encrypted upload. Large
installations can use the operational backup tools rather than silently truncating.

## Preview and restore

Owners upload a trusted `.coiny` file, provide its optional backup password, and
confirm their current account password. A linked file must belong to the current
household; a portable file may intentionally replace another household's finances.
Only compatible current schemas are accepted; unsupported backups fail without
silently upgrading or dropping fields.

Preview decrypts/authenticates first, then checks format, digest, identity, exact
schema objects against a pristine migrated database, migration history, SQLite
integrity/foreign keys, and every household-owned table. Unexpected tables, views,
triggers or cross-household rows are rejected. Uploaded database SQL is never run
as migration DDL. Private staging files are removed on normal completion/failure.
The preview displays backup date and current/restored account/transaction counts.
It does not replace any live financial data.

Preview tickets are encrypted with installation protection, bind the current user,
household, registry storage key and current snapshot digest, and expire in 15 minutes.
Old ticket files are pruned on later previews after 30 minutes. Neither password is
retained in a ticket. Confirmation requires a fresh current account password and an
explicit replacement checkbox. A changed household, expired/used ticket, removed
owner, or pending combined-preference operation requires a new preview/retry.

Before switching data, Coiny:

1. Creates a durable encrypted installation-linked safety backup of the current data.
2. Builds a new opaque SQLite file using trusted migrations, copies validated table
   rows, maps household IDs to the destination, and preserves financial IDs and
   deleted-ID high-water marks.
3. Keeps current central users/memberships/security state. Personal alert-read state
   is reset and operational preference receipts are not imported; source audit names
   and financial history remain. Unfinished imports may still refer to original
   actor IDs when moved across installations.
4. Publishes the closed/checkpointed file in WAL mode and atomically changes the
   PostgreSQL household registry only if the old key/ready state and owner membership
   still match.

The original SQLite file is never overwritten. All household POSTs and recurring
jobs use the writer gate; existing read requests can finish on the old file while
new requests use the new registry key. Refresh other open tabs after restoration.
A repeat confirmation cannot overwrite subsequent changes through the old ticket.
A PostgreSQL connection error can leave the outcome uncertain; inspect current
state before retrying. Published files are deliberately retained even on an ambiguous
commit acknowledgement, so a successfully committed registry cannot point at a file
that cleanup deleted. Pre-publication failures leave the original registry intact.

Safety downloads appear in Settings (latest ten shown), require owner authorization
and account-password confirmation, and can be previewed/restored through the same
workflow. Safety files, old SQLite files and unreferenced published candidates are
retained for recovery; production retention/disk monitoring and a maintenance-only
cleanup policy remain operational work. Do not delete files by guessing the active
key or remove recovery keys. Abrupt process/host failure can leave private staging;
encrypted disks and an operational cleanup policy remain necessary.

## CSV export

The download is a ZIP of UTF-8 CSV files plus a README. It includes readable
`AccountBalances.csv` and `TransactionLedger.csv`, and relational financial-table
files for budgets, savings goals/components/activity, rules, imports and historical
links. IDs link the files together. Cells are quoted and spreadsheet formula-like
text is prefixed to avoid executing imported merchant/description content as a
formula. Numeric signs and recorded amounts are preserved.

**CSV exports are not encrypted** and are not restorable `.coiny` backups. Store
and share them privately. Central identity tables, memberships and installation
metadata/keys are not exported. Owner/password checks apply to exports too.

## API and operational boundaries

`/api/household-data` exposes capabilities, backup/export POSTs, multipart restore
preview, explicit restore confirmation, and authenticated safety history/download.
Downloads use `Cache-Control: no-store`. Authenticated per-user rate limits and a
single in-process data-operation slot bound expensive work; upload sizes are capped.
Legacy storage cannot resolve the hybrid recovery service. The installation-wide
maintenance admission wrapper still applies to these routes.

This feature restores one household's finances. Full central/PostgreSQL recovery,
users/security recovery, scheduled/offsite backups, key custody, and old-node
isolation are covered by the [operator runbook](../tools/operations/README.md).
The [Docker setup](../Docker/README.md) configures Caddy HTTPS and proxy trust.
It does not implement client-held encryption or a zero-knowledge hosted service:
the server processes backup plaintext/passwords during the authenticated operation.

## Validation

Tests cover linked-key isolation, portable cross-key decryption, random encryption,
wrong password/tamper/truncation, CSV formula escaping, cross-installation and
cross-household-ID restoration, stale preview rejection, safety-backup recovery,
central-user preservation, member rejection, unexpected-trigger rejection, and
frontend confirmation/password cleanup. The operational migration/recovery suite
continues to run alongside these feature tests. Personal/rehearsal data must not be
used for destructive test restores or written to Git/build contexts.

Primary references:

- [Microsoft Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/).
- [OWASP password derivation guidance](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html).
- [OWASP cryptographic storage](https://cheatsheetseries.owasp.org/cheatsheets/Cryptographic_Storage_Cheat_Sheet.html).
