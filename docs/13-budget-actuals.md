# Planned versus actual allocations

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
Activity filters and pagination do not change the graph. A dashed teal forecast
continues the actual line through the end of the selected period or month.

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
Forecast includes these pending commitments without creating recorded activity;
individual/category assignments reduce outstanding amounts to prevent counting
the same assigned expense twice.

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
See [income-period budgeting](09-household-budgeting.md#monthly-and-paycheck-views).

Linked payments carry their scheduled due date as `paymentDueDate`. Item totals
include `priorPeriodActual` and `futurePeriodActual` for linked transactions whose
due dates fall outside the selected report range. Posting-date actuals, cash-flow
totals, and chart points remain unchanged. The UI shows “Payment for prior period”
or “Payment for future period” with its amount, and compares the current plan with
actuals excluding those other-period payments. Displayed remaining is therefore
`remaining + priorPeriodActual + futurePeriodActual` (rounded to cents). Genuine
current-period overspending still shows Over plan; contributions still use Above
target. Transaction details show the scheduled date so users can inspect the
timing difference. This also applies to monthly report boundaries.


## Forecast continuation

Daily points expose nullable `forecastSpending` and `forecastContributions`.
Forecast is null before the recorded-through date, equals actuals exactly on that
anchor date, then continues through the existing report end date. Entirely future
or completed reports have no continuation, and the chart does not manufacture one
from planned values when talking to an older API.

This is an estimate of allocations using actuals plus the remaining plan. It is
not a bank-balance forecast or spending-pace prediction. For a running remainder
across multiple pay periods, use the separate Running forecast view below.

- Linked scheduled payments replace their planned occurrences, including payments
  recorded in another period. A known linked entry dated in the future is estimated
  on its posting date using its actual amount.
- Unpaid scheduled bills/contributions remain on their due dates. Overdue items
  and manually paid items awaiting transactions are estimated on the next day;
  this is an assumption rather than a promised bank posting date.
- Individually assigned/category-assigned actuals reduce outstanding scheduled
  amounts in date order. Other-period linked payments do not reduce this period's
  commitments. Unassigned expenses remain actual spending but cannot safely be
  matched against an unpaid bill automatically; linking/assigning is needed to
  keep that estimate accurate.
- Remaining unscheduled allowances are distributed across future applicable days
  using their effective daily plan weights and exact cumulative cent rounding.
  A removed allowance is not spent after its applicable dates. Overspent allowances
  have no invented negative future spending.
- Generated savings goals remain on the income period's last day and are replaced
  by linked actuals once recorded. Monthly allowance forecasts also deduct linked
  goal payments so changing the view does not count them twice.

All three chart scopes share one scale per selected scope, including forecast
values. A teal dashed line meets the navy actual at identical coordinates; legends,
hover/touch/keyboard values, and an end-of-range summary identify the estimate.
No migration, ledger write, or payment-state change is required.


## Running forecast across pay periods

Choose **Running forecast** beside Allocation detail. The default horizon is three
months, with This period, 1 month, 3 months, 6 months, and 1 year options. The range
begins with the selected household income period and includes the complete period
containing the horizon end. Dates are displayed explicitly because a complete
period may extend beyond the requested number of calendar months.

This chart starts at $0 and shows cumulative income less spending and contributions.
It does not import an account opening balance or represent net worth, available
bank cash, or a savings fund balance. A +$500 first period and a -$200 second period
end at +$300. Planned and forecast remainders may be negative; the chart shows a
zero line and retains signed values rather than clamping shortfalls.

- Published household payday boundaries define the timeline, including combined
  same-day paychecks and future effective-dated budget changes. Month boundaries
  never reset the running total.
- Actual income uses household Income-category transactions on posting dates;
  transfers never create household income. Actual allocations use the same posted
  expenses and explicitly assigned/linked contributions as the allocation report.
- Paydays today or earlier use actual income transactions only. Missing or smaller
  deposits are not topped up to planned amounts, and extra income counts when it
  posts. Only strictly future paydays contribute planned income to the forecast.
  The plan remains unchanged for comparison; no paycheck confirmation is required.
  Explicit paycheck-to-transaction matching is not implemented.
- Forecast meets the current actual value exactly and carries each period's result
  forward. Future periods use the remaining-plan estimator, including already-paid
  occurrences, so early bill/contribution payments are not charged again later.
- Paired contribution transfers use the receiving entry's posting date in both
  actuals and forecast, even when the two banks post on different days.
- A linked payment for an item missing from the posting period's definitions is
  shown as a zero-planned item with its due-period definition. This keeps early
  transfers to newly introduced goals in actuals and avoids losing their impact.
- Entirely future ranges show a plan-based forecast with no invented actuals.
  Completed ranges show recorded results; missing historical imports are not
  filled in with planned income.

The view has planned/forecast/difference totals, horizon controls, a collapsible
assumptions section, and a scrollable per-period breakdown. Requests are canceled
when range or anchor changes. Allocation filters/pagination do not alter it;
refreshes and saved assignment/default changes do refresh it. The endpoint reuses
one published revision set and a monthly calendar cache while constructing the
range. It is read-only and needs no migration.


## Savings estimate for the selected range

Running forecast leads with Potential savings, Planned savings contributions,
Additional forecast remainder (or Funding shortfall), and Investment contributions.
Planned and forecast contribution amounts are shown separately when actual activity
changes the plan. The previous planned/forecast remainder comparison is retained in
a collapsible section; the chart continues to show additional remainder after
contributions, not the potential-savings total.

Savings and investments are summed by allocation type across the displayed complete
periods. Forecast contributions combine recorded assigned/linked contributions with
the remaining contribution estimate, including cross-period payments exactly once.
Payroll deductions and existing account balances are excluded.

- `additionalSavings = max(0, ending forecast remainder)`
- `fundingShortfall = max(0, -ending forecast remainder)`
- `potentialSavings = max(0, forecast savings contributions + ending forecast remainder)`
- `shortfallBeyondSavings = max(0, -forecast savings contributions - ending forecast remainder)`

For example, $3,000 in forecast savings contributions plus an $800 remainder is
$3,800 potential savings. A $200 shortfall instead leaves $2,800. If the shortfall
exceeds contributions, the UI shows $0 potential savings and the excess shortfall
explicitly. Investment contributions stay separate from these totals. Earmarked
savings goals are included; this is not a free-cash or savings-fund balance.

## Income-period headline and display details

The top **Period surplus** view uses the ending result from the running forecast
with `months=0` for the selected household income period. It combines actual posted
income and allocations to date with expected income and remaining allocations.
For completed periods it uses the final recorded result; future periods use the
remaining plan. A smaller **Planned remainder** shows planned income minus planned
allocations for comparison. Negative results display in red. Loading or failed
requests never substitute the planned number for a forecast; failures offer retry.
Payment changes, assignment/default saves, and refreshes update this estimate.
The monthly-plan view and allocation donut continue to describe the published plan.
The remainder is after savings/investment contributions, rather than total savings
or an account balance. The same expected-income assumptions described above apply.

Chart legends, tooltips, summaries, and accessible labels identify the selected
metric: planned/actual/forecast spending, contributions, allocations, or remainder.
Helper copy below controls and values has a small shared spacing adjustment.
The payment calendar selects today by default when today falls within the selected
income period. Other periods select their first day; payment refreshes preserve
an explicitly selected day.

The card also offers an account-based **Available cash** view. See [Available cash outlook](17-available-cash-outlook.md) for account selection, calculation boundaries, and API details.
