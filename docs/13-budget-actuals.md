# Planned versus actual allocations

[Documentation index](../README.md#documentation)

The budgeting dashboard defaults to comparing each published budget item with
recorded activity for the selected household income period. Monthly plan view
retains the monthly comparison. It shows **Planned**, **Actual**, and **Remaining** (planned minus actual),
alongside recorded totals and expenses not assigned to the budget. Negative
remaining amounts show spending over plan, or contributions above target.

The **Budget items** comparison starts expanded; category defaults and transaction
assignments start collapsed. Collapse the items when you want to focus on the graph;
the table shows about ten rows at a time and scrolls for additional items, keeping
its column headers visible. Click an item to open a paginated transaction breakdown
of its actual total, including posting dates, accounts, categories, and assignment
sources. This view is independent of the transaction assignment filters. Paid items
awaiting transactions remain separate from the recorded total.

The graph defaults to All allocations, matching the selected outlook’s planned allocation
total, and compares cumulative planned and actual amounts by day. Choose Spending,
Contributions, or All allocations; hover, tap, or use the arrow keys for daily
details. Scheduled items enter the plan on their due dates, while unscheduled
monthly allowances spread evenly across the month. Actual lines stop at the
recorded-through date. Spending includes unassigned expenses so missing assignments
do not hide spending; contributions require explicit assignments or payment links.
Activity filters and pagination do not change the graph. Month-end forecasting
remains a future increment.

## Dates and recorded activity

Actuals follow transaction posting dates within the selected period (or the
**transaction posting month** in monthly view). A November bill paid
on October 31 belongs to October's actuals while November retains the scheduled
plan. The current month includes activity through today in America/New_York;
past months include the whole month. Future months show the plan with zero actuals.
Cleared status does not exclude a recorded ledger entry.

Expense transactions are eligible regardless of account type, including credit
card purchases. Income is outside this allocation comparison. Ordinary transfers
are excluded from spending, and only count toward savings/investing when explicitly
assigned or linked to a scheduled contribution. A linked transfer is counted once,
on the receiving entry's posting date; either side can carry its assignment.
Unlinked single transfer entries use their own posting date.

## Assignment priority

1. An existing scheduled bill/contribution payment link identifies the budget item.
2. An individual transaction assignment overrides category defaults. An explicit
   **Leave unassigned** choice also overrides a category default.
3. Remaining expenses use their category's effective default.
4. Everything else stays unassigned. Unassigned transfers are visible for review,
   but are not included in the unassigned-expense amount or recorded budget totals.

No assignment is inferred from matching names or transaction descriptions. The
comparison is recalculated from the ledger, so corrections and deletions are
reflected on refresh. Assignment does not create transactions, change categories,
move money, alter snapshots, or mark a scheduled occurrence paid.

**Category defaults** apply from the selected budget month forward until superseded
by a later rule for that category. Earlier months retain their previous defaults.
Multiple categories can point to one budget item, such as Movies and Events both
counting toward Entertainment. Each category has only one effective default item,
so transactions are not duplicated across budgets.
Rules use stable allocation keys across budget revisions. If an item is no longer
in the selected month's plan, its activity stays unassigned rather than being
silently moved to a different item. Archived categories are hidden from the
defaults editor; their saved rules still classify existing transactions.

**Transaction assignments** default to unassigned expenses in the paginated activity
list. Transfers remain available through the type filter; scheduled contributions
are best linked through their calendar item. Choose
an item, leave the movement unassigned, or restore the category default. Transfers
only offer savings/investing targets. Activity assigned by a payment link must be
managed through the calendar's payment modal. To attach an individually assigned
movement to a scheduled payment, reset its individual assignment first. Two transfer
entries with separate payment/individual assignments cannot be linked until one
assignment is cleared; this prevents counting a single movement twice.

## Paid items awaiting transactions

Yellow paid-but-unlinked items remain separate from actuals. The comparison shows
their planned amounts, not an invented recorded payment. Linking the real
transaction makes it eligible for actuals in its posting month. The existing
manual-paid status and calendar indicators continue to work independently.
Future forecasting will need to distinguish those planned estimates from recorded
amounts and avoid overlapping them with activity already assigned by category.

## Storage, concurrency, and verification

`20261021170126_AddBudgetActualAssignments` adds `BudgetCategoryRules` and
`TransactionBudgetAssignments`; it does not rewrite financial records. Back up the
database and apply the migration before restarting the API. Override records are
removed when their transaction is deleted. Category rules retain effective-month
history. Both are household-scoped.

Category writes require the selected month's current rule version (zero for a new
rule in that month). Transaction assignments require reviewed transaction/pair edit
versions and advance those versions atomically; stale requests return 409. The
reconciliation fingerprint excludes these version-only updates, preserving financial
checkpoints when only a budget assignment changes.

Tests cover category-rule history, individual overrides/reset, payment precedence,
pending amounts, posting-month boundaries, future activity, transfer counting,
household isolation, stale writes, HTTP contracts, UI selection/paging/retry, and
responsive component compilation. See the [API reference](07-api-reference.md#budget-actuals).

Income-period comparisons use `/api/budgets/actuals/period?asOf=...`; scheduled goal
set-asides enter the planned line on the household period's last day. Monthly
spending allowances use exact prorated portions. Cross-month category rules are
resolved per transaction posting month; category rule editing remains monthly.
See [income-period budgeting](09-household-budgeting.md#income-period-budgeting).
