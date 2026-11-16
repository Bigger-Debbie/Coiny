# Account reconciliation

Select an individual account on **Accounts**, then choose **Reconcile Account**.
Enter the statement ending date and balance. For a credit card or loan, enter the
amount owed as a positive number; a credit balance can be negative.

Coiny compares the statement with the opening balance plus all transactions through
the end of that date. Later activity is excluded. The calculation uses the same
asset/debt and transfer signs as current account balances; daily chart snapshots
are not used as the source of truth.

If the amounts differ, correct missing/duplicate transactions or the opening
balance before confirming. An optional adjustment is available when you cannot resolve the difference. **Review these transactions**
shows the account's full history from its opening date through the statement date,
clearing list filters while leaving the trend chart's range alone. Return to
reconciliation to reuse the statement inputs and run a fresh check.

Matching amounts still require an explicit confirmation that the statement
transactions have been reviewed. Saving a checkpoint does not change transactions,
their cleared status, transfer links, bill payments, or balances. This is a record
of verification, not a backup or a restore point.

## Saved checkpoints and subsequent changes

Checkpoints belong to the household and record the statement date/balance,
confirmation time, and confirming user's name/ID snapshot. The account overview
shows its latest statement checkpoint; the modal lists the latest 50, ordered by
statement date, confirmation time, and ID. Older history is retained in storage.

Changes to transactions on/before a checkpoint date mark it **Needs review**.
This includes later imports of old activity, additions, deletions, edits, moving
transactions between accounts/dates, and changes to cleared status or recorded
details. Opening balance/date changes also invalidate affected account checkpoints.
Changing only an account name/institution or linking/unlinking existing transfer
entries preserves verification because the checked ledger entries are unchanged.
Activity strictly after a checkpoint date leaves it verified.

Invalidation is persistent: reverting an edit does not silently restore an old
confirmation. Check the statement again and save a new checkpoint. Original
records retain their review flag and reason. Ledger writes and checkpoint flags
commit or roll back together, including atomic imports and transfer edits.

The server rechecks the ledger inside the confirmation transaction using a
fingerprint of the opening balance/date and statement-period transaction data.
A stale check returns 409, even when a detail change leaves the balance unchanged.
Repeated confirmation of the same still-valid statement ledger returns the
existing checkpoint. No financial data is rewritten by the migration.

## Implementation and checks

- Schema: `20261021123659_AddAccountReconciliations` adds the checkpoint table
  and indexes; apply it before restarting the updated API.
- API: [reconciliation contracts](07-api-reference.md#account-reconciliation).
- Backend tests cover statement-date boundaries, debt signs, explicit review,
  stale checks, household isolation, idempotency, invalidation, rollback, transfer
  linking/amount edits, account opening changes, and authenticated HTTP contracts.
- Frontend tests cover comparison/mismatch states, explicit confirmation, stale
  errors, history, debt labels, and review-period navigation/input restoration.


## Optional reconciliation adjustment

Review missing, duplicate or incorrect transactions first. On a mismatch, choose
**Use a reconciliation adjustment instead** to preview its statement date, balance
change and resulting statement balance. Provide a reason, confirm that you reviewed
the transactions, then explicitly acknowledge the adjustment.

**Create adjustment and reconcile** atomically creates a dated **Reconciliation
adjustment** transaction, rebuilds balances, and saves a matching checkpoint. It
preserves opening balances and later transaction amounts. Credit-card/loan changes
use Coiny's positive-amount-owed convention. The separate system category is excluded
from income, spending and individual budget assignments.

A changed ledger or target requires another check. Retrying the same operation does
not create another adjustment. History retains the amount, reason and transaction
link. Adjustment audit details cannot be edited through ordinary transaction editing;
remove the adjustment and reconcile again to correct it. Removal invalidates affected
checkpoints while preserving their recorded adjustment amount and reason.

Existing hybrid installations need the `ReconciliationAdjustments` household migration:
stop the API, take a verified backup, and run the new API's
`--upgrade-households --offline` before restarting. Legacy SQLite installations use
the corresponding CoinyContext migration. No control-schema migration is required.
