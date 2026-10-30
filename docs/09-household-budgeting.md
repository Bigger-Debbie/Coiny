# Household budgeting

A household shares one budget. Initial setup creates a draft; publication makes
it available to the monthly and income-period views. Later edits happen in the
Budget breakdown, without repeating setup.

## Setup and publication

The seven steps are Income, Housing & bills, Subscriptions, Everyday spending,
Investments, Savings goals, and Review. Savings comes last so affordability checks
can use the rest of the plan.

**Save & continue**, **Back**, and **Save & exit** save the complete draft, including
other steps. Unsaved edits do not change the saved preview. Leaving with unsaved
changes asks for confirmation; navigation is blocked during a write.

Drafts can be incomplete. Publication requires valid income, completed entries,
and a current/future effective month. Over-budget plans are allowed but show their
shortfall. A stale edit version requires reload, not an automatic overwrite.
Published revisions are immutable. A future-only plan opens at its first effective
month rather than restarting setup. Existing drafts must be finished before
published-item edits, so the two flows cannot overwrite each other.

## Amounts and schedules

Income is take-home money per payday. Scheduled allocations are amounts per
occurrence; unscheduled allocations are monthly amounts. For example, $2,000 paid
every two weeks means $4,000 or $6,000 in a month according to actual paydays.
A $600 unscheduled grocery allowance stays $600 for the month.

Payroll health insurance and retirement amounts are optional visibility fields.
They are already deducted from take-home income and do not reduce it again.
Amounts use decimal arithmetic; input must be nonnegative, have at most two decimal
places, and fit the supported monetary range. Published income must be positive.

| Frequency | Fields and behavior |
| --- | --- |
| Weekly / EveryTwoWeeks | StartDate anchors the cycle; no monthly day fields |
| Monthly | StartDate bounds the schedule; DayOfMonth 1–31 |
| TwiceMonthly | DayOfMonth and a later SecondDayOfMonth, both 1–31 |
| EverySixMonths / Yearly | StartDate anchors the original month/day |

EndDate is optional; bounds are inclusive. Missing month days clamp to the last
day without changing the original recurrence. Weekends stay as entered. Two
monthly slots that clamp to the same date remain two occurrences.

## Monthly and paycheck views

The default **Income period** covers all household paydays on one date through the
day before the next distinct payday. A bill on the next payday belongs to the next
period. The individual-income schedule selector is also available in monthly mode.
Missing prior/next paydays show an explanation instead of an invented period end.
Periods can cross months and effective-dated budget changes.

Scheduled items enter a period on their due date. Monthly allowances are prorated
using differences of rounded cumulative daily amounts so adjacent periods sum
correctly. Unscheduled Savings/Investments create period-end set-asides; ordinary
Spending does not create a payable occurrence.

The monthly view includes all of the selected month's applicable published plan.
Navigation retains the existing panels while fetching, then fades them back in.
The month arrows align with the input below its label. Stale controls are disabled;
out-of-order responses cannot replace the newest selection.

The income/allocation cards show the plan. The remainder card uses actuals plus
remaining commitments; a smaller planned figure provides comparison. **Available
cash** can instead start from selected account balances. See
[actuals and forecasts](13-budget-actuals.md) and [available cash](17-available-cash-outlook.md).

## Allocation chart and breakdown

The donut shows Housing, Bills, Subscriptions, Everyday spending, Savings,
Investing, and the remainder. Each group has a fixed distinct color; green is
reserved for the remainder. Empty groups disappear. If allocations exceed income,
percentages use total allocations and the center reads **Total planned**; no extra
income/shortfall slice is invented. Payroll deductions stay outside the donut.

The breakdown has six collapsible sections: Income, Housing & bills, Subscriptions,
Everyday spending, Investments, and Savings goals. Income starts open. Headers show
counts/totals; desktop contents scroll within the chart's height, while stacked
layouts cap the breakdown height. Hover, tap, focus, and keyboard interaction work
on the chart. Reduced-motion preferences disable its motion.

## Edit an existing budget

Click an item or **Manage all items** to edit, add, remove, or move definitions.
Choose an effective date (today by default), review monthly/period impact, then save.
Earlier occurrences keep their earlier definitions; removal stops future occurrences
without deleting ledger entries or payment history. Moves retain the stable source key.

A save creates a new immutable published revision. Monthly allowances are prorated
across dated segments. Older first-of-month revisions keep their original behavior.
Paid future occurrences retain their name, amount, due date, section, and identity
in ProtectedPaymentsJson. Rescheduling a paid occurrence suppresses its replacement
at the corresponding monthly ordinal instead of charging it twice.

Updates use the latest reviewed edit version. Conflicting changes or a separately
scheduled later revision require review; Coiny does not silently replace them.
See [item-update contracts](07-api-reference.md#published-budget-item-updates).

## Calendar and payments

The calendar defaults to today when it lies in the selected range. It shows
income, bills, contributions, awaiting links, and linked payments. Selecting a day
opens its events. **Show completed** includes/excludes completed occurrences.

**Mark paid** records a date/note; **Link transaction** selects existing activity;
**View payment** reviews it. Status and ledger links are independent:

- Unpaid: upcoming, due today, or overdue.
- Paid, awaiting transaction: reserves the planned amount in forecasts but has no
  recorded actual. Its transaction might already appear as unassigned spending.
- Paid, linked: actuals use linked posting dates and amounts.
- Savings can also be skipped for a period or covered by existing fund allocations.

Up to 50 transactions can link to one occurrence, including split utility charges.
Bills/subscriptions use expenses; contributions may use expenses or transfers.
A movement can belong to only one occurrence, and paired transfer sides count once.
Candidates prioritize nearby dates/amounts and can be filtered; matching is explicit.
Conflicting individual assignments must be cleared before linking.

Unlinking or deleting a transaction preserves manual paid status and other links.
Unchecking paid detaches links. These actions do not create bank entries or move
cash. Marking paid settles the occurrence independently of the linked total; there
is no automatic partial-payment balance. Awaiting totals are separate from unpaid
bill/contribution totals. Actuals and forecasts do not count settled plans twice.

For savings goals, existing cash, contribution schedules, and skipping, see
[savings funds](19-savings-funds.md). Recurring suggestions are reviewed separately;
see [detection](14-recurring-detection.md).

## Model and API

HouseholdBudget owns BudgetRevision rows, each with income sources, allocations,
and owned schedules. Stable SourceKey values preserve item/payment identity across
revisions. Constraints enforce one draft per budget, unique revision numbers,
valid states/schedules, and optimistic edit versions.

See [API payloads](07-api-reference.md#household-budgeting),
[storage architecture](21-hybrid-storage-and-azure.md), and
[backend tests](../Coiny.API.Tests/README.md).
