# Recurring expense detection

## Manual review

On the budget page, **Recurring activity → Scan transactions** checks recorded
household expenses. Nothing runs automatically on page load. Results are independent
of the selected outlook period: the scan uses up to 30 months through today in
America/New_York. The panel shows the date range and expense count.

Suggestions show merchant, typical charge, amount range, cadence, last charge,
estimated next date, and up to 12 supporting transactions. The next date is an
estimate from the last observed charge, not a scheduled bill; it may already be
past if a charge was missed or has not been imported. At least three charges are
required, including yearly patterns, so annual detection needs two years of history.

- **Add to budget** opens the existing editor with merchant, median amount,
  frequency, and estimated next date filled in. Subscription is the initial type;
  users can choose a bill instead, correct the schedule, and review before saving.
  Effective-date and paid-history protections still apply. After saving, scan and
  confirm the matching budget item if needed.
- **Confirm match** associates the pattern with an existing current scheduled bill,
  housing item, or subscription. This records coverage only; it does not categorize
  transactions, mark bills paid, or attach charges to individual occurrences.
- **Dismiss** remembers the review for the household. Similar scans do not repeatedly
  flag it. A median amount change exceeding the larger of $2 or 20% reopens it;
  a different cadence has a separate identity. **Clear review** removes this saved
  decision. Existing independent payment links remain evidence of coverage.
- Covered and dismissed results are hidden by default and available using a toggle.
  A confirmed target that no longer exists in the current scheduled plan is no
  longer treated as coverage. Exact normalized budget-name matches only suggest a
  target; payment links to a single current item provide stronger coverage evidence.

## Detection rules and limits

The detector considers positive Expense-category transactions only. It excludes
income, transfers, refunds, and future entries. It groups by merchant, falling back
to description when merchant is absent. Unicode case and punctuation are normalized;
digits and full merchant identities are preserved. Variable bank reference strings
can prevent grouping; this conservative first pass does not strip arbitrary numbers.

Supported consecutive-day-gap tolerances are weekly 7±2, fortnightly 14±3,
monthly 30±5, six-monthly 182±14, and yearly 365±21. All observed gaps must fit.
Same-day duplicate charges make a group ambiguous and are not suggested. Missing
cycles, mixed services at one merchant, and irregular charges may not be detected.
Patterns more than two intervals plus tolerance behind today are omitted.
Amounts must remain within the larger of $5 or 75% of the median, allowing variable
utilities while rejecting very wide ranges. Typical amount is the median rounded
to cents. Regular shopping can still resemble a subscription; all results require
review. Calendar-based next dates are estimated from the last charge, so weekend
posting shifts may affect them. No confidence score promises certainty.

## Storage and deployment

Migration `20261024163633_AddRecurringExpenseReviews` adds only
`RecurringExpenseReviews`, unique by household and SHA-256 merchant/cadence key.
It stores dismissals, confirmed budget keys, and the reviewed typical amount.
Scans are read-only and recompute suggestions from current ledger and plan data.
Review writes are household scoped and never alter ledger, account balances,
reconciliation checkpoints, or budget/payment records. Optional daily scanning and persistent alerts are described below.

Apply the migration before scanning and restart the API for the new controller.
Tests cover cadence recognition, conservative exclusions, household isolation,
review persistence, amount-change reopening, target validation, HTTP access checks,
manual scan behavior, errors, and explicit budget review with prefilled fields.

## Optional automatic scans and alerts

Under **Avatar menu → Settings → Preferences → Recurring detection · Household**, enable
**Scan daily for recurring expenses**. It is off by default. Any household member
may change it; it is shared with the household, while the mortgage preference
remains personal. Both toggles are staged until **Save settings** in the shared footer. Personal and
household changes save together in one transaction; **Reset changes** discards unsaved edits.
Stale setting versions return a conflict and require reload. The last successful
scan date appears below it.

A hosted API worker checks enabled households on startup and every 15 minutes.
It runs at most one successful scan per household per America/New_York calendar
day, persisting that date so restarting the API does not repeat the scan. Failed
scans are retried on a later check, and one household failure does not stop others.
The API must be running; this is not an external cron or email service. Disabling
stops future automatic scans; existing alerts remain available. Manual scans
remain on demand and read-only, and do not consume the daily scan or create alerts.

Only NeedsReview patterns generate active household alerts. One persisted alert
per household/pattern is updated across repeat scans. Unchanged patterns do not
become unread again; a typical amount change over the larger of $2 or 20%, or a
resolved pattern reappearing, creates a new unread version. A changed cadence has
its own pattern identity. Covered, dismissed, or no-longer-detected patterns resolve
existing alerts. Confirming or dismissing a pattern resolves its alerts immediately;
other ledger/plan changes are reflected at the next automatic scan. The worker
rechecks settings and saved reviews inside its write transaction.

**Avatar menu → Alerts** opens persistent suggestions. A blue badge on the avatar,
Alerts menu item, and mobile hamburger shows the number unread, capped visually at
99+ on icons. Counts refresh every minute during an authenticated header session.
Opening the list does not mark items read. **Mark read** updates only the current
member; **Review pattern** marks that visible version read and opens/scans the
budget recurring panel. Dismissal/matching is a separate household review action.
Read requests carry a version so an old view cannot clear a newer notification.
Resolved alerts are retained as metadata but omitted from the active list/count.
The list is paginated at 20; routine action toasts remain separate.

Migration `20261025141830_AddRecurringAutomationAndAlerts` adds
`HouseholdRecurringSettings`, `HouseholdAlerts`, and `HouseholdAlertReads`. It changes
no existing financial tables. Read receipts belong to individual users and are
household scoped through their alert. Settings and receipt concurrency prevent
stale updates. No external notification provider is needed.
