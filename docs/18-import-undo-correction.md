# Import review, correction, and undo

Open **Import history** from Accounts, the CSV review's recent-imports area, or the
import completion screen. Accounts can pass their selected account as a filter.
The page lists the most recent 50 file imports, retaining original dates, filenames,
counts, account names, and undo counts.

Choose **Review import** to see remaining ledger transactions from that file. Click
a transaction for the existing detail/edit modal; correction uses normal transaction
management and recalculates history. It does not change the original import record
or reconstruct earlier versions. Rows already deleted are absent. A multi-file
session can be reviewed as one file or all of its files; relationships crossing
files remain visible in either scope.

## Select and preview

Unchanged entries are selected initially. New imports record immutable entry IDs,
edit versions, and transfer pair IDs after all import-time matching completes.
Subsequent edits or pairing changes require explicit selection. Older imports have
no original-state baseline; their rows are clearly flagged for review and are not
selected automatically. No baseline is invented from today's ledger.

Use row checkboxes, Select unchanged, Select all, or Clear selection. Search only
filters the visible list; it does not remove hidden rows from the selection. Preview
shows the exact removal count, before/after account balances, transfer pairs to
detach, preserved counterparts, individual budget assignments to remove, payment
links to detach, and currently valid reconciliation checkpoints affected.

Final confirmation requires acknowledgments for edited/unknown rows, detached
payment links, and affected reconciliation checkpoints. Preview fingerprints bind
confirmation to the selected imports/transactions and current affected account
ledgers, opening balances, transfer relationships, payment links, assignments, and
checkpoint records. Changes after preview require a fresh preview.

## Undo behavior

- Delete only explicitly selected imported ledger entries. Never silently remove a
  pre-existing or unselected transfer counterpart. Unpair surviving counterparts,
  preserving their amounts, dates, accounts, and balances. Both sides are deleted
  only if both are selected (for example, when undoing the full multi-file session).
- Detach payment links that reference removed entries. Bills/contributions keep
  their independent paid state and all other links. With no links left, the item
  becomes Paid / Awaiting transaction; actuals no longer include removed entries.
- Remove individual budget assignments belonging to deleted transactions.
- Recalculate snapshots from each affected account's earliest removed posting date.
- Preserve reconciliation history. A valid checkpoint with statement date on/after
  a removed transaction becomes Needs reconciliation with an import-undo reason.
  Earlier checkpoints and already-invalidated history are left alone.
- Keep the original batch and append an audit operation containing the actor, exact
  reviewed effects, removed transaction states, affected accounts, and result.
  An undo is not an automatic restore mechanism.

Ledger deletions, pair changes, checkpoint invalidation, balance snapshots, batch
undo counts, and the audit record commit in one transaction. A failure rolls them
all back. The same operation ID and request can be retried without repeating the
undo. Reusing an operation ID with a changed request is rejected. Replaying an
original import/session after any member file was undone is rejected; re-importing
requires a newly reviewed submission, not resurrection by retry.

## API and migration

All routes require authentication and current household membership:

- `GET /api/transaction-imports/{batchId}/review`: current rows, related session
  files, default safe selection, preview effects, and prior undo operations.
- `POST /api/transaction-imports/undo/preview`: `batchIds` (1–8 distinct household
  files), nullable `removeTransactionIds` (null selects only unchanged known rows).
- `POST /api/transaction-imports/undo`: explicit IDs, `operationId`,
  `reviewedFingerprint`, and acknowledgments `acknowledgeChangedTransactions`,
  `acknowledgePaymentLinks`, `acknowledgeReconciliations`.

Validation failures return 400, stale/reused requests 409, and unauthenticated calls
401. Review changes nothing. History responses additionally expose `undoneCount`,
`lastUndoneAt`, and an optional `accountName`.

`20261026191419_AddImportUndoAudit` adds baseline/undo metadata to import batches
and a separate undo audit table. It does not alter financial rows, create baseline
history for existing imports, or execute any undo. Back up the database before
migration and restart the API afterward.

Tests cover atomic rollback, stale previews, idempotent retries, original submission
replay blocking, household isolation, edited/legacy selection, session pair handling,
pre-existing counterpart preservation, retained paid status and other payment links,
checkpoint date boundaries, snapshot recalculation, authenticated routes, and the
review/correction/confirmation UI.
