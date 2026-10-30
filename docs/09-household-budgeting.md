# Household Budgeting and Scheduling

[Documentation index](../README.md#documentation)

## Current increment

The backend models, migrations, recurrence calculator, and household-scoped
draft/publish/month-projection services and authenticated HTTP endpoints are implemented.
The frontend setup wizard and published allocation dashboard are implemented. Established budgets are managed directly from the breakdown; see “Manage an established budget” below.
The current-income-period bill list, monthly calendar, and bill occurrence payment tracking are implemented.
Dated item editing, actuals/forecasts through one year, available cash, and
account-backed component savings goals are also implemented. See
[actuals/forecasts](13-budget-actuals.md), [available cash](17-available-cash-outlook.md),
and [savings funds](19-savings-funds.md).
The existing account/category breakdown is a summary of recorded transactions;
it is separate from the planned-income allocation chart described here.

## Agreed product rules

- One plan belongs to a household and is shared by its members.
- Setup is based on whether the household has completed a budget, rather than
  whether a particular user is new. Incomplete setup can be resumed.
- Support multiple take-home income sources, with weekly, every-two-weeks,
  twice-monthly, monthly, every-six-months, and yearly schedules.
- Monthly expected income comes from actual scheduled paydays in that month,
  including three-paycheck months for every-two-weeks schedules.
- Allocate income to housing, bills, flexible spending, savings, and investing.
- Remaining income is Unassigned until explicitly allocated.
- Over-allocation can be saved and must show the amount over expected income.
- Schedules describe plans, not completed payments. No automatic ledger entries,
  payment completion, or automatic transaction matching are implied. Payment status is recorded explicitly per bill occurrence.
- Monthly day numbers that do not exist in a month clamp to its last day.
  Keep the original day number for following months.
- Keep weekend dates as entered. Business-day adjustments are future work.
- Edits apply from a chosen month, defaulting to the current month. Preserve
  earlier plan versions for history.

The backend now implements the calculation and draft/publication rules below.
The wizard presents saved draft previews; the summary presents published monthly plans.

## Model

| Type | Stored information |
| --- | --- |
| `HouseholdBudget` | One household-owned root with its revisions |
| `BudgetRevision` | Revision number, edit version, effective first-of-month date, Draft/Published state, saved setup step, publication timestamp |
| `BudgetIncomeSource` | Name, take-home amount per occurrence, optional health-insurance/retirement amounts already deducted per paycheck, required schedule |
| `BudgetAllocation` | Name, allocation type, amount, optional schedule |
| `BudgetSchedule` | Frequency, start/anchor date, optional inclusive end date, applicable monthly day numbers |

Each revision is a complete version containing its own income sources and
allocations. It is not a list of differences. A single draft can coexist with
published versions. Revision numbers are unique within a household plan;
multiple published revisions may share an effective month so edits can take
effect within the current month without overwriting the earlier version.

The saved wizard step is Income, Bills, Subscriptions, Spending, SavingsAndInvestments, or
Review. Publication is represented by Published state and a timestamp, rather
than a separate completed wizard step.

Allocation types are Housing, Bills, Subscriptions, Spending, Savings, and Investments. These
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
| EverySixMonths / Yearly | StartDate anchors the original month/day; both monthly day fields are absent |

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
and schedules for Housing/Bills/Subscriptions allocations. Other allocations may be unscheduled.
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

Optional `HealthInsurancePerPaycheck` and `RetirementContributionPerPaycheck` record
amounts already deducted before take-home pay. They may be omitted/null or zero;
when provided they follow the same nonnegative, two-decimal precision rules as
other amounts. They are saved, returned, and copied into later drafts. Monthly
`HealthInsurance` and `RetirementContributions` totals multiply each amount by
actual scheduled paydays, both per source and household-wide. They do not reduce
ExpectedIncome or Unassigned, increase Allocated, or create extra calendar events.
Tax withholding and employer contributions are not modeled by these fields.

Six-month/yearly schedules use the original start month/day on every occurrence.
For example, August 31 every six months clamps to February 28/29 and then returns
to August 31. February 29 yearly returns to February 29 in leap years. Bounds are
inclusive and weekends stay unchanged. The full payment belongs to the due month;
monthly saving toward it is a separate savings allocation.

The projection creates no transactions. Published bill events include separately recorded payment state; draft previews remain planning-only.

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
`AddPaycheckDetailsAndExtendedBudgetSchedules` adds two nullable income columns
and expands the schedule checks. Existing definitions are preserved with null
paycheck details. Rolling back this migration removes those details and requires
removing or changing six-month/yearly schedules first, since the older schema
does not accept them.
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
3. A resumable household setup wizard and its API — implemented.
4. The monthly income-allocation donut with Unassigned/over-budget presentation — implemented.
5. Calendar and current-income-period bills/contributions — implemented.
6. Budget overview/editing with effective-date changes — implemented.
7. Forecasting and plan-versus-actual reporting — implemented.
8. Account-backed savings component goals and affordability reviews — implemented.


## Frontend setup and review

The authenticated `/budget` route opens the household's setup status. New plans
start only when **Set up your budget** is selected. **Continue setup** resumes the
shared saved step and definitions. Published plans use the **Budget breakdown**
and **Manage all items** for dated edits; they do not send users back through the
initial wizard. Existing drafts can still be completed before item edits. A future-only published
plan is shown for its first effective month; it does not restart onboarding.

The seven steps are Income, Housing & bills, Subscriptions, Everyday spending,
Investments, Savings goals, and Review. Income is per paycheck; bills and
subscriptions use schedules. Spending is monthly; investment contributions can be
monthly or scheduled. Savings goals come last so their affordability review can
use the rest of the draft. See [savings funds](19-savings-funds.md).
The **Budget starts in** month input lives in the saved-draft summary panel
throughout setup. Step-specific examples and guidance explain take-home pay,
estimating changing bills, flexible monthly spending, and avoiding double-counting
payroll savings deductions. Weekly/every-two-weeks schedules use **First payday** or **First payment date**
to identify the repeating cycle. Six-month/yearly schedules also use that first
date and do not show monthly day fields. The optional **Paycheck details** section
records health insurance and retirement contributions per paycheck, explicitly
already deducted from take-home income. Monthly/twice-monthly schedules use **Schedule
starts on** for the earliest included date and separate monthly payday/payment-day
fields, with visible explanations and examples. Past drafts can still be saved,
but must move to the current/future month before publication.

**Save & continue**, **Back**, and **Save & exit** save the complete draft,
including entries from other steps. Back also saves before moving. Empty steps
can be saved; incomplete individual rows must be completed or removed. This first
version uses explicit saves rather than autosaving each keystroke.

The running summary is explicitly a **saved draft preview**, calculated by the
API for the saved effective month. Unsaved edits display a notice and do not
change the totals until saved. The summary panel and month input remain visible
while a small loading message replaces the totals during preview refreshes. Review shows per-source income and per-allocation
monthly amounts. **Save review** records final changes; **Publish budget** is
disabled while edits are unsaved, the month is past, or the preview is unavailable.
The backend checks completeness at publication and allows over-budget plans.

Leaving with unsaved edits asks for confirmation; refresh/close uses the browser's
standard warning. Navigation is blocked during a write. Logout clears the session
only after navigation succeeds, so canceling the leave warning keeps the session.
Stale or missing drafts block further writes and offer **Reload household draft**;
reloading asks before discarding unsaved edits. The client never retries a stale
save by silently replacing its edit version.

Publication opens the income-period outlook with an optional Monthly plan view.
Planned calendar occurrences do not create transactions or imply completion.
The calendar and period panel are described below; actuals and forecast behavior
are documented in [budget actuals](13-budget-actuals.md).


## Published monthly dashboard

Published households open to **Your income-period outlook**. **Monthly plan**
switches to **Your monthly budget**. Item editing stays in the breakdown; there
is no bottom Edit budget button.
The initial month is the current America/New_York month when a plan applies, or
the first published effective month for a future-only plan. Previous/next controls
and a month input request `/api/budgets/month`; historical and future months are
supported. Established item changes default to today and require reviewing their
effective-date impact. Existing initial-setup drafts resume their saved effective month.

Cards show take-home income, planned allocations, and either unassigned money or
the amount over budget. The donut groups planned allocations into Housing, Bills,
Subscriptions, Everyday spending, Savings, and Investing, plus Monthly remainder.
Green is reserved for the remainder; each other group has a distinct fixed color.
Group IDs and colors
stay stable when groups disappear in different months. Zero-valued slices are
omitted; no-payday/no-allocation months show an empty message.

When allocations fit within income, slice percentages use take-home income as
the denominator and include the remainder. When allocations exceed income, the
center reads **Total planned**, percentages use total allocations, and an explicit
warning reports the shortfall. There is no invented extra-income or shortfall
slice. Payroll health-insurance and retirement details are shown separately;
they do not enter the donut or reduce take-home income again.

The shared donut retains hover/focus enlargement, tooltips, tap/keyboard pinning,
Escape dismissal, and reduced-motion support. Budget mode uses unsigned amounts,
monthly plan labels, and no transaction counts. Account category charts retain
their signed income/expense amounts and transaction counts.

The **Budget breakdown** pane has six native, keyboard-accessible collapsible
sections: Income, Housing & bills, Subscriptions, Everyday spending, Investments,
and Savings goals. Income starts expanded; the other sections start collapsed,
with totals and item/source counts visible in each header. On desktop, the
breakdown matches the donut card height and its contents scroll independently
under the fixed heading. Stacked mobile layouts cap the breakdown height instead. Paycheck deductions are inside Income. Expanding sections
lists income by source/payday count and all defined allocations,
including zero totals for items not due in that month. These are planned amounts,
not actual spending. Six-month/yearly amounts appear in due months only. Loading,
retryable request errors, and months without an applicable published revision
have explicit states. Out-of-order responses cannot replace the newest selected
month, and draft changes remain separate until published.


## Current income period and monthly calendar

The dashboard places a **Current income period** panel beside a Monday-first
calendar for the selected dashboard month. The period is anchored to today in
America/New_York, independently of the month being browsed. Household income is
the default; a custom selector can follow one published income source instead.
That choice survives month navigation.

A period starts on the latest applicable published payday on/before the reference
date and ends the day before the next distinct payday. Same-day household paydays
form one boundary. Bills due on a payday belong to the period starting that day.
Published revisions are resolved by effective month throughout the lookup, so a
period can cross months and account for future published schedule changes.
Drafts never influence this view.

**Remaining unpaid bills** sums Housing/Bills/Subscriptions events in that range that are not marked paid. The total scheduled amount stays visible;
scheduled Savings/Investments appear separately as contributions. These are all
household bills even when following one income source: no paycheck-to-bill
assignment is implied. Unscheduled monthly spending is excluded from the dated
list. Bill status is Upcoming, Due today, Overdue, or Paid based on its due date and recorded payment state; it does not establish affordability.

The lookup searches twelve calendar months before/after the reference month,
covering every supported recurrence. Without an earlier published payday or a
known next payday, show the corresponding explanation instead of inventing a
period end or summing an arbitrary open-ended range. Future-only budgets can
still be browsed on the calendar while there is no current published income period.

Calendar days mark income, bills, and contributions; intersecting current-period
days are shaded. Hover/focus shows scheduled details (Escape dismisses the tooltip);
click, keyboard activation, or touch selects a day and lists every item below the
grid. Empty days explicitly show no scheduled items. Mobile uses the selected-day
list instead of a hover popup. **This month** returns the shared month navigation
to the current America/New_York month.

Income definitions now carry a persistent `SourceKey`, unique within a revision.
It survives wizard saves and draft cloning. Omit it for a new source; API clients
must retain returned keys when updating existing sources. Duplicate/empty keys
are rejected. `AddBudgetIncomeSourceIdentity` backfills a distinct key per existing
row without guessing matches between historical copies or duplicate names. New
revisions preserve identity explicitly; legacy copies are not retroactively linked.
No ledger schema or transaction data is changed by this migration.


## Bill and contribution payments and transaction links

Select **Mark paid**, **Link transaction**, or **View payment** beside a bill or scheduled savings/investment contribution in the current income period
or the calendar's selected-day list. A modal records a payment date and optional
note. An optional transaction picker suggests household expense entries near the
due date, prioritizing matching amounts; search includes description, merchant,
account, and category across all dates. Selecting an entry marks the occurrence
paid by default. Contributions can also link to transfers, selecting either the
outgoing or incoming entry. Transfer candidates show account and direction.
Linking an already-paid item preserves its payment date.

Payment status and ledger linkage are independent. Unlinking keeps the paid
status. Unchecking paid detaches the transaction on save. Neither operation creates
ledger entries or changes account balances. The basic payment states are:

- **Unpaid:** normal appearance and upcoming/due/overdue labels.
- **Paid · Awaiting transaction:** a yellow rounded border and clock marker;
  **Link transaction** opens the picker directly while preserving the payment date.
- **Paid · Linked:** a green rounded border and checkmark, with **View payment**.

Savings also supports **Skipped this period** and **Funded · Allocated** states;
these exclude skipped commitments or recognize existing fund allocations without
inventing new bank activity. See [savings funds](19-savings-funds.md).

These indicators appear in the current income-period list, selected-day details,
calendar markers/tooltips, and payment modal. Removing a link or deleting its
transaction returns a still-paid item to awaiting status. Previously marked-paid
items without a link derive the same yellow state without rewriting stored data.
The income-period summary counts awaiting items and totals their planned amounts;
they remain excluded from unpaid bills/contributions. Actuals use linked
transactions on their posting dates; the forecast reserves paid-unlinked planned
amounts until linked. Such transactions may already appear as unassigned spending,
which is why the remainder card explains awaiting links.

A transaction may be
linked to one scheduled occurrence. The two sides of a linked transfer count as
one movement and cannot be assigned to separate occurrences. Linking two
previously unlinked entries requires resolving any conflicting payment assignments
first. Contribution-linked transfers remain editable; deleting the ledger entry
detaches it while preserving the independent paid status.
Marking paid settles the occurrence independently of the linked total. Up to 50
transactions can be linked to one payment, including split utility charges.
Automatic settlement based on a partial-payment balance is not implemented.
Housing/Bills/Subscriptions link expense entries; contributions can link qualifying
expenses or transfers. Matching remains an explicit user decision.

Income-period contributions retain their separate scheduled total and expose a
remaining unpaid contribution total. They do not inflate the bills total. Only
Housing, Bills, Subscriptions, Savings, and Investments payment occurrences are payable;
unscheduled monthly Savings/Investments create period-end set-asides, while
Spending and payroll deduction visibility fields do not have payment occurrences. Existing payment storage/routes are reused, with no
new schema migration or financial-data rewrite.

Occurrences use household + allocation `sourceKey` + scheduled date. Allocation
keys survive wizard saves, renames, and new revisions. New definitions omit the
key; updates retain it. Duplicate/empty keys within a revision are rejected. The
migration assigns distinct keys to legacy allocation rows without guessing that
similarly named rows across old revisions are the same bill. Changing a scheduled
date creates a different occurrence; payment history is not moved automatically.
Removing a bill from the plan does not delete its payment record. Deleting a linked
transaction detaches it while retaining payment status.

## Subscriptions

Subscriptions have their own wizard step after Housing & bills and their own
breakdown and donut group. They use recurring bill schedules (including yearly
renewals), payment tracking, transaction links, and posting-date actuals. Existing
bills remain unchanged; use the allocation-type selector when editing a draft to
move a subscription into the new group. Publish the draft to apply the change.
`20261021214401_AddBudgetSubscriptions` extends the database enum constraints;
existing enum values and records are preserved.

### Moving and removing budget items

Use **Budget section** in an item's wizard editor to move it among housing, bills,
subscriptions, everyday spending, savings, or investing. This preserves its stable
allocation key and schedule; scheduled bill types receive a schedule if missing.
The item appears in its destination wizard step. Save and publish to apply it.

Publication releases transaction links for payment occurrences that no longer exist
in the applicable published plan (including removal or a changed due date), or whose
transaction type no longer fits. Changing an item's section preserves compatible
links when its identity and occurrence remain intact. Draft edits never release
live links. Earlier-month links remain when the plan for that month still contains
the occurrence. Released payments retain their paid date and notes; no ledger entry
or balance is changed. Their transactions become available for linking again.

### Browsing income periods

Previous and Next move to the date before the selected period starts and its next
payday respectively, using the existing `income-period?asOf=` API. Current returns
to today's period. The selected household/individual income schedule is preserved.
Period navigation updates bills, contributions, totals, and calendar shading without
changing the overall monthly outlook selection.

A period within one month shows that full month. A period spanning months shows
both full months, side by side on wide screens and stacked on smaller screens.
Longer periods (for example annual income) show each covered month. One shared day
details area supports dates and payment actions from every displayed month.
Calendar months and payment states reload after payment changes, retaining the
selected period. Incomplete periods still show their available month and explain
why dates or navigation are unavailable. No forecasting amounts are added.

The remaining income is labeled **Potential savings** in the monthly totals,
wizard summary, and donut. Its segment is consistently green and uses a thinner
ring while preserving the correct percentage of total income.

## Income-period budgeting

The dashboard defaults to a household income-period outlook. One top-level period
selector drives income, allocations, signed expected remaining, donut, breakdown,
planned-versus-actual graph, transaction drilldown, and calendar. Monthly plan view
remains available for reviewing definitions and monthly totals. The wizard still
defines monthly amounts and scheduled due dates. The earlier reserve prototype is
superseded by the shared period range.

Each period begins on a distinct household payday and ends before the next one;
paychecks arriving together are combined. Bills/subscriptions and explicit
contributions use their scheduled dates. Monthly allowances are split using
rounded cumulative month fractions: the portion for days a–b is rounded(monthly ×
b/daysInMonth) minus rounded(monthly × (a−1)/daysInMonth). This preserves every
month's exact cents, even across mixed pay schedules and month boundaries. A
cross-month period combines its portions of both monthly plans. Published revisions
for each covered month are respected. Definitions remain unmodified.

Unscheduled monthly Savings/Investments produce **Planned set-aside** events on the
last day of the household period. A $500 car goal yields $225.81 for Oct 2–15,
$225.80 for Oct 16–29, and $232.26 for Oct 30–Nov 12 when November is also $500.
October 1's $16.13 belongs to the earlier period covering that day. Days before the
first published payday have no complete income period; the UI explains the missing
boundary rather than inventing income. The monthly view still includes the entire
monthly definition.

Generated set-asides are included once in allocations and the calendar. They are
not automatically paid and never create ledger entries. Existing mark-paid/link
controls apply, with yellow awaiting-transaction and green linked states. Explicit
contribution schedules keep their original dates. Generated identities use the
stable allocation key plus household period end date, and publication cleanup
validates those identities before detaching links. Individual income views group
the same household goal events rather than creating alternative duplicate dates.

Actuals use transaction posting dates within the selected range, including effective
category rules from each posting month. Transaction overrides save against their
posting month; category defaults remain monthly. Negative expected remaining means
that the period needs funds carried forward. It is not silently reduced to zero.
Expected remaining is after planned savings/contributions and is not the total
amount saved. Running-balance forecasting and carried fund balances remain roadmap
work. This change needs an API restart but no database migration or financial data
rewrite.

## Manage an established budget

The wizard is used for initial setup. Once published, the Budget breakdown offers
clickable income/allocation rows, Add actions within sections, and Manage all items
for definitions without an occurrence in the selected outlook. An existing saved
wizard draft can still be finished; item edits are blocked until it is published
so the two editing flows cannot silently overwrite each other.

Item details open a shared-style modal. Edit or remove an item, choose an effective
date (today by default), review its income-period and monthly impact, then save.
Removal stops future occurrences; it never deletes transactions or paid history.
Section moves retain the same stable source key. Saving creates an immutable
published revision behind the scenes, without sending the user to the wizard.

`EffectiveFromDate` supplements the existing first-of-month revision boundary.
Old revisions retain their original monthly behavior. Monthly projections are
composed from dated segments; period projections use the same segments. Monthly
allowances are prorated by differences of rounded cumulative daily portions, so a
midmonth edit affects only days on or after its effective date. Earlier scheduled
dates and paydays continue to use the earlier definition.

Paid future occurrences are captured in `ProtectedPaymentsJson`, retaining their
planned amount, due date, name, section, and stable identity even if the definition
is renamed, moved, rescheduled, or removed. A rescheduled paid occurrence suppresses
its replacement at the corresponding ordinal within that month to avoid counting
the same scheduled payment twice. Paid generated set-asides also retain the daily
allowance portions that funded them. Payment status and links remain read from the
payment table; the snapshot does not create or alter transactions.

To avoid overwriting a separately scheduled future revision, subsequent changes
must start on or after the latest published effective date. The editor displays
this earliest date. The reviewed revision ID and an atomic edit-version reservation
reject concurrent/stale saves. Household scoping applies to reads, previews, and
publication. Preview performs no persistent writes.

Schema migration: `20261023162702_AddEffectiveBudgetDates` adds a nullable effective
date and protected-payment JSON to revisions. Existing definitions and financial
records are retained.

## Data switching and layout

Month and income-period switches retain the mounted budget cards/panels while new
data loads, dim them, and fade them back over 180 ms. Stale interactive regions
are temporarily inert. Monthly arrows align with the month field, below its label.
These are local transitions and do not replay the full coin page overlay; reduced
motion disables the fade. See [frontend transitions](08-dashboard.md#in-page-data-transitions).
