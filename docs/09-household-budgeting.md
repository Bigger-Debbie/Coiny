# Household Budgeting and Scheduling

[Documentation index](../README.md#documentation)

## Current increment

The backend models, migrations, recurrence calculator, and household-scoped
draft/publish/month-projection services and authenticated HTTP endpoints are implemented.
The setup wizard and budgeting dashboard remain to be built.
The existing account/category breakdown is a summary of recorded transactions;
it is separate from the planned-income allocation chart described here.

## Agreed product rules

- One plan belongs to a household and is shared by its members.
- Setup is based on whether the household has completed a budget, rather than
  whether a particular user is new. Incomplete setup can be resumed.
- Support multiple take-home income sources, with weekly, every-two-weeks,
  twice-monthly, and monthly pay schedules.
- Monthly expected income comes from actual scheduled paydays in that month,
  including three-paycheck months for every-two-weeks schedules.
- Allocate income to housing, bills, flexible spending, savings, and investing.
- Remaining income is Unassigned until explicitly allocated.
- Over-allocation can be saved and must show the amount over expected income.
- Schedules describe plans, not completed payments. No automatic ledger entries,
  paid/overdue claims, or transaction matching are implied.
- Monthly day numbers that do not exist in a month clamp to its last day.
  Keep the original day number for following months.
- Keep weekend dates as entered. Business-day adjustments are future work.
- Edits apply from a chosen month, defaulting to the current month. Preserve
  earlier plan versions for history.

The backend now implements the calculation and draft/publication rules below.
The wizard and dashboard will present these results in later increments.

## Model

| Type | Stored information |
| --- | --- |
| `HouseholdBudget` | One household-owned root with its revisions |
| `BudgetRevision` | Revision number, edit version, effective first-of-month date, Draft/Published state, saved setup step, publication timestamp |
| `BudgetIncomeSource` | Name, expected take-home amount per occurrence, required schedule |
| `BudgetAllocation` | Name, allocation type, amount, optional schedule |
| `BudgetSchedule` | Frequency, start/anchor date, optional inclusive end date, applicable monthly day numbers |

Each revision is a complete version containing its own income sources and
allocations. It is not a list of differences. A single draft can coexist with
published versions. Revision numbers are unique within a household plan;
multiple published revisions may share an effective month so edits can take
effect within the current month without overwriting the earlier version.

The saved wizard step is Income, Bills, Spending, SavingsAndInvestments, or
Review. Publication is represented by Published state and a timestamp, rather
than a separate completed wizard step.

Allocation types are Housing, Bills, Spending, Savings, and Investments. These
describe planned uses of income. They do not replace transaction categories or
automatically link allocations to accounts/categories.

### Amount semantics

An income source's amount is per payday. An allocation without a schedule is
a monthly allocation; an allocation with a schedule is an amount per occurrence.
For example:

| Definition | Intended monthly projection |
| --- | --- |
| Take-home pay of $2,000 every two weeks | $4,000 or $6,000 according to that month's paydays |
| Rent of $1,500 monthly on the 15th | $1,500 allocation and one calendar event |
| Groceries of $600 without a schedule | $600 allocation and no scheduled event |
| Investing $100 weekly | $400 or $500 according to that month's occurrences |

Monthly totals and calendar events are derived from the same definitions.
Do not store a separate estimated monthly total for a scheduled item.
Amounts use C# decimal and SQLite TEXT mapping to preserve decimal round trips.
The draft service rejects negative amounts, more than two decimal places, and
amounts exceeding 9,999,999,999,999,999.99. It does not silently round entries.
Zero amounts are allowed in drafts; published income sources must be positive.

### Schedule fields

| Frequency | Required fields beyond Frequency |
| --- | --- |
| Weekly | StartDate anchors the weekday; monthly day fields are absent |
| EveryTwoWeeks | StartDate anchors the two-week cycle; monthly day fields are absent |
| TwiceMonthly | StartDate bounds the schedule; DayOfMonth and SecondDayOfMonth are 1–31, with the second greater than the first |
| Monthly | StartDate bounds the schedule; DayOfMonth is 1–31 and SecondDayOfMonth is absent |

EndDate is optional and cannot precede StartDate. A schedule is an EF owned
value stored on its income/allocation row, not a separately managed record.
`BudgetScheduleService` generates one month's occurrences using the original
recurrence definition. Date bounds are inclusive, weekly cycles continue across
month/year boundaries, and weekend dates stay unchanged. If two twice-monthly
slots both clamp to the same short-month date, both occurrences are retained:
they remain two planned payments, not one deduplicated event.

## Draft and publication services

`IBudgetService` is implemented by `BudgetService`:

| Method | Behavior |
| --- | --- |
| `GetSetupStatusAsync` | Read current month, publication status, first effective month, current published revision ID, and resumable draft |
| `PreviewDraftAsync` | Project the saved draft for a requested applicable month, checking its edit version without publishing |
| `GetDraftAsync` | Read the current household's saved draft, or null |
| `StartDraftAsync` | Resume an existing shared draft; otherwise create one for the selected month, copying the applicable published plan |
| `SaveDraftAsync` | Replace the draft's full income/allocation definitions and save its setup step atomically |
| `PublishDraftAsync` | Validate and publish the draft without changing older published versions |
| `GetMonthAsync` | Resolve a published revision and calculate income, allocations, unassigned/over-budget totals, and scheduled events |

Household identity comes from `ICurrentUserService`. Foreign draft IDs and
attempts to save/publish an already published version are rejected.
Starting setup again resumes the existing household draft rather than replacing
another member's work, even if a different target month was requested.

Save and publish require the response's current `EditVersion`. A successful save
or publication increments it. Stale versions raise `BudgetConflictException`;
the API returns 409 so the wizard can reload the draft and let the user reconcile changes.
An atomic version check and database transaction protect definition replacement.
A failed child write rolls back the version change and all replaced rows.

Drafts may have empty income/allocation lists. Stored individual entries still
need valid names, amounts, and schedule fields. Publication requires Review step,
at least one positive income source (all income source amounts must be positive),
and schedules for Housing/Bills allocations. Other allocations may be unscheduled.
Over-budget plans and unassigned income are allowed.

New drafts default to the current month in America/New_York. New draft creation
and publication cannot target a past month. A draft that survives a month rollover
can still be resumed/saved; choose a current/future effective month before publishing.

## Monthly revision selection and results

For the requested month, select the Published revision with the latest effective
month on or before it; break same-month ties using the highest revision number.
Drafts never influence published projections. A previously scheduled future
revision stays in effect when an earlier-month revision is edited.
The service never updates a published version through its draft operations.

Without an applicable published revision, return a null revision ID and empty,
zero-valued results. Otherwise:

- ExpectedIncome sums take-home amount times actual payday count.
- Allocated sums monthly unscheduled amounts plus scheduled per-occurrence amounts.
- Unassigned is the positive remainder; OverBudget is the positive excess.
- Scheduled events contain date, item ID/name, income/allocation classification,
  and amount. Unscheduled allocations have zero occurrences and create no events.

The projection creates no transactions and does not mark anything paid or overdue.

## Storage constraints and migration

The database enforces:

- One budget root per household and at most one Draft revision per budget.
- Positive, unique revision numbers within each budget.
- Nonnegative edit versions, also configured as EF concurrency tokens.
- An effective date on the first day of a month.
- Draft revisions without a publication timestamp, and Published revisions with one.
- Recognized wizard steps, allocation types, and schedule frequencies.
- Names of 1–100 trimmed characters, nonnegative amounts, valid recurrence fields,
  and an end date on or after the start date.
- Required schedules for income and fully absent or valid schedules for allocations.
- Restricted deletion of referenced household plans, revisions, and their parents.

The foundation migration creates four budget tables and their indexes/constraints.
`AddBudgetDraftEditVersion` adds the edit-version field/check. Neither migration
alters financial ledger definitions, creates sample budgets, or resets data.
See [migration setup](06-development-setup.md#restore-and-initialize) for the
explicit database-update command.

Publication validation, revision selection, and household access are enforced by
the service. The schema alone does not make published rows immutable against
direct database writes. See the [budget API contract](07-api-reference.md#household-budgeting)
for routes, payloads, and response codes.

## Repeatable verification

`Coiny.API.Tests` contains xUnit recurrence, SQLite workflow, and migration tests.
Run `dotnet test Coiny.API.Tests/Coiny.API.Tests.csproj` from the repository root.
The tests use isolated in-memory databases and a controlled clock, with no API
server, JWT key, or live database required. See the
[test guide](../Coiny.API.Tests/README.md) for coverage.

## Delivery sequence

1. Models, EF configuration, and reviewed migration — implemented.
2. Recurrence calculation and revision/draft workflows, including household access
   checks and publication validation — implemented.
3. A resumable household setup wizard — API implemented; frontend next.
4. The monthly income-allocation donut with Unassigned/over-budget presentation.
5. Calendar, upcoming scheduled items, and pay schedules.
6. Budget overview/editing with effective-month changes.
7. Forecasting and plan-versus-actual reporting later.
