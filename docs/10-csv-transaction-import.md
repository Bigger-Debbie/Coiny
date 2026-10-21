# CSV transaction import

[Documentation index](../README.md#documentation)

Work is isolated on `feature/csv-transaction-import`, based on the merged account
transfer feature. Accounts → Import CSV opens a lazy-loaded upload and mapping
and review screen. Raw file contents are read locally in the browser. Reviewed
normalized rows are sent to the authenticated validation/import API; the raw CSV
and source account-number columns are not stored. Confirmed batches create
transactions, daily snapshots, and import history atomically.

## Agreed workflow

1. Choose one account and a CSV file.
2. Confirm column mappings, date format, and money-in/out conventions.
3. Review transactions, assign a specific category to every included ordinary
   transaction, and explicitly classify transfers. Bulk category assignment and
   inline category creation should make this efficient.
4. Resolve possible duplicates with Skip or Import anyway; exclude or correct
   invalid rows. Pending entries are excluded by default.
5. Confirm the reviewed batch and save its entries and snapshots atomically.

No included transaction may precede its account's opening balance date. Users
need an opening balance representing the start of the imported history to avoid
counting transactions twice. Transfer imports create only the selected account's
entry; linking a corresponding entry later does not move money again. Unknown
and external counterparties are allowed. Matching suggestions require confirmation.

The backend independently validates account/category ownership,
dates, amounts, category/direction consistency, row limits, duplicate decisions,
and an idempotent submission token. Browser validation alone is not sufficient.
Import history retains batch metadata (filename, date, count, identifier).
Leaving the mapping screen warns before discarding its file and preview; draft persistence
and undo are deferred.

## Implemented foundation

`Coiny.Web/src/app/features/transactions/import-transactions` contains the mapping
screen and pure parsing utilities. Tests cover multiple unrelated export layouts,
arbitrary headers, locale formats, preambles, encoding recovery, and account filtering.

- Up to 2,000 transaction records, 2 MB of UTF-8 text, and 100 columns.
- Comma, semicolon, or tab delimiters; automatic delimiter suggestion or explicit
  selection. Quoted values, escaped quotes, multiline descriptions, BOM, CRLF/LF,
  and physical source line numbers are preserved. Inconsistent column counts or
  malformed quoting are rejected with actionable errors.
- Explicit header-line selection skips bank-specific introductory summaries.
  UTF-8, Windows-1252, and UTF-16 little-endian decoding are available. A decoding
  failure can be retried with another encoding without reselecting the file.
- Column suggestions use header names, never account numbers or private values.
  Date format and signed-amount polarity are deliberately not guessed.
- Explicit MM/DD/YYYY, DD/MM/YYYY, YYYY-MM-DD, MM-DD-YYYY, DD-MM-YYYY,
  DD.MM.YYYY, or YYYYMMDD date interpretation; impossible dates are rejected
  rather than normalized into another month.
- Two-digit years are supported as MM/DD/YY, DD/MM/YY, MM-DD-YY, DD-MM-YY,
  and DD.MM.YY. The explicit century rule is 00–69 → 2000–2069 and
  70–99 → 1970–1999; the mapping screen explains it when a short-year format
  is selected. Four-digit and two-digit formats are not silently interchanged.
- Separate money-out/money-in columns, one signed amount column, or an amount
  plus a bank-specific direction indicator. Users assign each indicator value
  to In/Out; unassigned values are flagged. Explicit split-column roles determine
  direction even when debit values are negative.
- Dot-decimal/comma-thousands and comma-decimal/dot-thousands formats can be chosen
  explicitly. Dollar prefixes and parenthesized negative amounts are supported.
  Ambiguous formats, zero amounts, and excessive precision are flagged. This does
  not add currency conversion; Coiny still uses its existing USD presentation.
- Pending statuses default to excluded; original classification is retained as a
  suggestion, with common HTML entities decoded for display. Long descriptions
  are flagged rather than silently truncated. An exact active household category
  name/type match is suggested during review; there is no fallback generic category.
- Optional posting-status mapping lists distinct values and lets the user exclude
  any bank-specific pending/unposted values. Missing optional columns are allowed.
- Multi-account files can be filtered by a mapped account identifier; a selection
  is required when that column contains different accounts. Account identifiers
  never automatically select the Coiny destination.
- The screen shows source samples and requires account/date/direction confirmation.
  Preview navigates to a separate review view at `/accounts/import?step=review`.
  Back to mapping preserves the file, mappings, and live row edits. Returning to
  review reuses those edits if nothing changed; changing a mapping resets them.
  A refresh cannot restore the in-memory file and returns to the mapping step.
- Text is treated as data, never evaluated as HTML or spreadsheet formulas.

The supplied AccountHistory export was checked locally without copying personal
data into the repository: 56 rows, 54 included posted rows, 2 excluded pending
rows, 28 preserved Transfer classifications, and no mapping errors when the
opening date is October 1, 2026 and the selected date format is MM/DD/YYYY.

## Next milestones

- Confirmed transfer matching across imports, followed by saved mapping conveniences.

## Review and batch persistence

Users can edit dates, descriptions, merchants, directions, and amounts; include or
exclude rows; filter by description/classification; and assign categories individually
or to selected rows. Bulk assignment spans all filtered rows, not just the visible
page. Income/Expense categories are restricted to the corresponding money direction.
Transfer is an explicit choice and accepts Unknown or External counterparties.
Inline category creation is available. Included rows default to cleared, with a
batch checkbox to change that. Pending exclusions can be changed explicitly.

The review is a bounded spreadsheet-style pane with sticky column headers and
row/status columns, cell borders, alternating light-gray rows, and pagination
in groups of 20. Yellow rows need a category or duplicate decision; red rows have
invalid data. Ready rows use a subtle green status indicator rather than a full
green background; excluded rows are muted. Text/icon statuses accompany colors.
Click a status to expand its explanation and duplicate decisions below the grid.
Recent imports are collapsed by default.

Cells display values until activated. Text, number, native date, and shared custom
dropdown editors appear on click. Enter commits, Escape cancels, and Tab/Shift+Tab
commits and moves between editable cells. Read-only bank classifications stay plain
text. Edits to date, amount, description, or direction reset that row's previous
duplicate override. Headers and buttons follow the site's body font and control
styles; panel headings keep the thinner account-page weight.

Check categories and duplicates sends included rows to the backend. Matching uses
the same account, calendar date, exact decimal amount, money direction, and a
case/whitespace-normalized description. Matching category/merchant is not required.
The check flags existing ledger entries and repeated rows within the selected
batch. Each flagged row must be skipped or explicitly marked Import anyway.
These are potential matches; legitimate repeated purchases can be kept. Duplicate
IDs are capped at five examples per row. Editing invalidates final validation.

When checks pass, the summary shows account, count, transfer count, and the current
and projected balance (amount owed for liabilities). Explicit confirmation enables
Import. The backend repeats all checks inside a transaction, creates the batch and
its entries, creates snapshots for each imported date, and recalculates once from
the earliest date, including later stored history. Failure rolls everything back.

Transactions retain ImportBatchId/ImportRowId for attribution; imported transfers
never create the other side automatically. The account's last 50 import batches
are shown after review and completion. History is household-scoped.

Submission IDs are unique per household. Replaying the same normalized payload
returns its original result; reusing an ID with different data returns 409. After
an uncertain network/server failure, the UI locks edits and retries the identical
payload and ID. If commit-time validation returns 422, updated row checks are shown
and editing is reopened. Leaving while saving is blocked; successful completion
allows navigation without a discard warning. Starting another file generates a
new submission. A repeated file in a new session is checked against the ledger
rather than rejected solely by its filename.

Apply `AddTransactionImportBatches` before using the import API. Payload examples
and HTTP responses are in the [API reference](07-api-reference.md#transaction-imports).


## Optional bank balance verification

Checking and savings imports can map a **Posted running balance** column. Common
Balance/Running Balance/Ledger Balance/Posted Balance headers are suggested;
Available Balance is deliberately not suggested. The user can choose **ignore**
to import without a comparison. Other account types do not offer this check yet.

Confirm newest-first or oldest-first order, including the original order within
one date. Also confirm the balance is posted balance **after** each transaction
and the export covers all posted activity through its final date, rather than an
available balance or an incomplete selection. Pending/status-excluded rows do not
supply the reference. Multi-account source filtering happens before reference
selection. Invalid date order, a missing final balance, or invalid balance number
format requires correcting the mapping or ignoring the balance column.

This first pass compares **one closing balance**, using the original final posted
source row on the latest date. It does not claim to reconcile every intermediate
running balance. The reference stays fixed if review later edits/skips that row,
including when skipping an already-imported duplicate. Skipping a missing posted
entry can therefore reveal a difference rather than silently moving the reference.
Included edited transactions cannot have dates later than the reference date.

The server calculates opening balance + every existing ledger change through the
reference date + included import changes. Transfers count toward account balance
without becoming income/expense. Skipped duplicates are not sent again; their
existing ledger entries are already counted. Later Coiny transactions affect the
normal projected current balance but are excluded from the dated bank comparison.

The review summary shows the date, bank balance, Coiny calculated closing balance,
and **bank − Coiny** difference. Matching checks have a green border; mismatches
require acknowledgment plus the usual final import confirmation. Acknowledgment
is bound to the exact calculated balance reviewed. Import repeats the check in
its database transaction; a newly changed difference returns 422 for fresh review.
Safe retries retain the exact original request and reference.

No opening balance is changed, adjustment entry created, or bank number treated
as authoritative ledger data. The comparison is optional, and a deliberately
acknowledged mismatch can be imported. It is not yet a persistent reconciliation
statement/history or a credit-card sign conversion feature.

### Reconciling transfers after separate account imports

Import each file into its own Coiny account and classify the relevant rows as Transfer (with Unknown or External counterparty). After both imports, open either entry from transaction details and choose **Reconcile transfer**. Select the other account and link the matching existing entry. Coiny does not add another ledger entry or change either balance. Different bank posting dates are preserved; widen the date window when needed. Review ambiguous matches manually. Use **Create the other side** only when that counterpart is not recorded; it adds one transaction to the selected account. Do not use Add Transaction to recreate a movement already imported. For importing several accounts together, use the multi-account session described below.

## Multi-account import sessions

The Accounts page’s **Import CSV** action opens one unified importer at `/accounts/import` for one or multiple files. `/accounts/import-session` redirects there, retaining the selected account.

The unified session accepts one or multiple files. File-switching controls appear only for multiple files. Transfer matching is skipped when there are no suggestions; a single file can still match existing Coiny entries. The flow has these focused steps:

1. **Files:** upload up to eight bank exports and choose a different Coiny account for each. Limits: 2 MB and 2,000 rows per file, 8,000 included rows per session. This first version accepts one file per account.
2. **Review each file:** map that file’s columns, then use the existing spreadsheet review for categories, exclusions, duplicate decisions, and optional bank-balance checks. Each bank can have its own format. Files are reviewed individually; switching files preserves edits. Nothing is imported at this step. Source samples and advanced file options are collapsible.
3. **Match transfers:** review equal-amount, opposite-direction transfers within seven days, comparing the uploaded accounts and existing unlinked household transfers. Cards show both accounts, descriptions, dates, and whether an entry is new or already recorded. Nothing is selected automatically. Selecting a pair disables competing suggestions using either entry. Suggestions are paginated; at most 200 are returned. Remaining/unmatched transfers can be linked later from transaction details.
4. **Confirm:** review per-account projected balances, new transaction counts, selected pairs, and unmatched transfer entries, then confirm the session. Selected matches only connect ledger entries; they never create another counterpart. Monthly/weekly reconciliation can therefore import Bills and Spending together without duplicating a transfer.

All account batches and accepted links commit in one transaction. A file validation failure or stale match prevents the entire session from being imported. A response that cannot be confirmed locks the final request; **Retry this session safely** sends the exact same payload. A stored household-scoped session receipt returns the original result after a successful retry. Changing a completed session’s payload under its original submission ID returns 409. File-level import history and original row provenance are retained.

The new `TransactionImportSessions` table stores only an idempotency receipt, not the raw CSV files. Apply the `AddTransactionImportSessions` migration before using session imports. Single-file imports use the same session validation, matching, and atomic commit methods.

### Review grid sizing

The spreadsheet scrolls horizontally inside the review panel; filters and actions remain within the page width. Drag a column-header divider to resize that column within its minimum/maximum width. Focus a divider and use Left/Right arrows (Shift for larger changes), Home, or End for keyboard resizing. Double-click a divider to reset that column; **Reset column widths** resets all columns. Description and Category have wider defaults. Width changes preserve transaction edits, selections, and validation checks. Header and row-selection columns remain pinned while scrolling; Status is unpinned on narrow screens to leave room for data. Widths remain with that file’s in-memory review while navigating between files.
