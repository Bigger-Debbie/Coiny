# Savings funds and component goals

Savings accounts can hold several purpose allocations. Each fund belongs to exactly
one household savings account and has a stable SourceKey. Scheduled funds use
that same key for their budget allocation.
The account balance remains ledger-derived; earmarking cash never creates or edits
a bank transaction. Fund balances are the sum of their allocation activity.

## Funds without scheduled contributions

Choose **Funding → Add money when I choose** for emergency savings or other cash
that does not need a paycheck contribution plan. Name the fund, choose its savings
account, and enter money already set aside. A single purpose can use the fund name;
add components only if you want separate balances inside it.

This mode has no deadline, recurring amount, budget item, or calendar contribution.
It works before a budget is published. Review shows the opening allocations, then
**Save fund** creates the fund. **Add money**, **Assign existing savings**, moves, and
withdrawals remain available; allocations cannot exceed unallocated account cash.
Manual fund additions stay out of budget contribution actuals and forecasts.
Fund-backed expenses retain the normal savings-funded spending treatment.

Funding style is fixed after creation; existing scheduled funds keep their plans.
The API stores `plan.scheduleContributions: false` with zero-amount Monthly
components and no target dates. Omitted values default to scheduled behavior for
existing funds. No schema migration is needed.

## Scheduled funds

- Select a savings account on Accounts. Its fund makeup replaces the expense donut.
  The stacked bar/list shows funds, unallocated cash, and any allocation shortfall.
  Fund details keep Paycheck contribution and Use fund money visible; Manage fund
  groups editing, existing-cash assignment, allocation moves, and archiving.
- Create/manage funds from the budget page's Savings funds & goals section, or from
  the final planning step in the initial wizard. Investments has its own step.
- Adopt an existing savings budget item instead of adding a duplicate. Its stable
  identity, earlier budget versions, and payment history remain intact. No existing
  savings items or bank balances are converted automatically.
- Assign opening cash from the account's unallocated amount, then define one or more
  components: a dated target, a recurring cost (monthly/quarterly/six-month/annual),
  or a monthly reserve. Already funded cash reduces the initial amount needed.
- Invalid components are highlighted with inline field messages; Review focuses the
  first issue. A valid review opens a separate dialog, with Back to components
  preserving all entries.
- Review component amounts and total contribution for each household income period.
  The schedule covers at least a year and extends to later targets (up to ten years).
  Contributions are due at period end, or earlier if a component deadline falls
  inside the period. Cumulative rounding preserves exact cents across boundaries.
- The review replaces the existing goal's future contribution when checking capacity.
  Published plans use actuals for the current period and forecasts for future periods;
  drafts use saved income schedules and allocation definitions. Negative periods are
  highlighted, with supported monthly capacity and an indicative combined target
  completion date when available. A stretch goal needs explicit acknowledgment.
- Save updates the fund and budget contribution atomically. Draft contribution changes
  remain unpublished until the draft is published. Opening account-purpose assignments
  are real immediately; a draft does not move money into that account.

## Recording and using money

A planned contribution is not a funded balance. Record a contribution against cash
already in the savings account, optionally linking the incoming deposit/transfer.
A calendar contribution linked to the receiving transfer automatically allocates its
amount across the fund's components; unlinking reverses those allocations, provided
later withdrawals have not consumed them. One bank movement cannot fund two goals.

Assign existing savings is a purpose-only allocation and does not count as a paycheck
contribution. Monthly reserves may use a zero contribution when the fund only
organizes an existing balance.

Recording a paycheck contribution can settle the matching calendar occurrence. It appears as
Funded / Allocated rather than awaiting a bank transaction. A partial contribution
reduces the forecast commitment but does not settle the entire occurrence.

Withdrawals can link all or part of an expense from any household account. Link the
expense rather than a transfer back to spending. The expense remains visible at its
full amount in transaction reports. Its funded portion is excluded from current
paycheck spending in the budget outlook so savings are not charged twice. A transfer
out may temporarily make purpose allocations exceed the recorded account cash until
its expense/withdrawal is allocated; the account view explicitly shows this shortfall.

Components can exchange allocations without bank movements. Money can also move to
another fund in the same account or be released to unallocated cash. Ordinary activity
can be reversed with retained original/correction history; calendar activity is corrected
through its payment link. Empty funds can be archived, stopping upcoming contributions
and retaining history. Dated target schedules account for current funded balances and recorded contributions
so extra allocations reduce what remains to contribute. Earlier income periods retain
their plan. Unmet overdue targets remain reserved in the current period rather than
disappearing from the outlook.

## Data integrity and operations

- All endpoints require authenticated household membership and server-side ownership.
- Account, component, sign, amount, date, and available-cash checks are server-side.
- Version checks protect concurrent fund edits and budget changes. Allocation activity
  and moves use operation identifiers to protect retries.
- Budget revisions store immutable savings plan JSON snapshots, preserving historical
  contribution definitions. Existing saved enum values remain unchanged; Investments
  and SavingsGoals were appended to BudgetSetupStep.
- Linked financial edits require first correcting the fund activity. Removing a ledger
  entry preserves its purpose-allocation audit and raises a missing-link warning in
  fund activity. Import undo includes fund links in its reviewed dependencies.
- Migrations AddSavingsFunds and SplitSavingsWizardSteps add fund/activity tables,
  nullable BudgetAllocations.SavingsPlanJson, and widen the wizard-step constraint.
  They do not create funds, infer opening allocations, or rewrite financial data.
- Fund balances are bookkeeping allocations, not live bank balances or separate assets.
  A goal's capacity estimate is based on the recorded household budget and known
  actuals; it does not guarantee future income or replace an account reconciliation.

## API

- GET /api/savings-funds?accountId={optional savings account}
- POST /api/savings-funds/preview — account, components, existing budget/draft version
- POST /api/savings-funds — atomic fund + contribution-plan save
- GET /api/savings-funds/{key}/candidates?kind=Contribution|Withdrawal
- POST /api/savings-funds/{key}/activity — contribution, withdrawal, component reallocation
- POST /api/savings-funds/{key}/activity/{operation}/correct?expectedEditVersion={version}
- POST /api/savings-funds/{key}/move — another fund in the same account, or unallocated
- POST /api/savings-funds/{key}/archive — empty fund and expected budget/fund versions

Tests cover exact goal/cycle rounding, adoption and historic preservation, household
isolation, drafts, funded expense actuals, calendar linking/unlinking, concurrent edits,
retry behavior, allocation moves, correction history, deadlines/completion, stretch-goal
acknowledgment, and the shared user interface.

## Skipping a savings contribution

Open a savings contribution in the payment calendar and choose **Skip this period**.
Its stored household income-period boundaries suppress that savings item's planned
amount across the full period, including both portions of a cross-month period.
The zero-valued occurrence stays visible as **Skipped this period**, with a restore
control. Period/month allocations, planned chart points, remaining commitments,
forecast contributions, and the remainder calculation exclude the skipped amount.
Investments and bills cannot be skipped with this action.

Skipping creates no bank transaction or fund activity and does not erase actual
money already recorded. Linked payments must be unlinked explicitly first. Existing
partial fund contributions stay in actuals while the remaining commitment is skipped.
Use **Assign existing savings** separately for money supplied from another source.
Restoring clears the skip and reinstates the contribution. Later dated-goal funding
may need adjustment after a missed period. The AddSkippedSavingsContributions migration
adds status and boundary fields to BillPayments without rewriting existing records.

The budget summary adapts by width: wide income-period views stack the income and
allocation cards beside the cash/remainder card; narrower views place the remainder
below them. Wide monthly views keep all three summary cards at an equal height.

For scheduled funds, the activity form asks whether money comes **From this
paycheck** or **From existing savings**. Paycheck contributions enter budget
actuals even without a transaction link. Existing savings only increase the
fund’s allocation; they do not mark a scheduled contribution paid. Use this
option for catch-up funding from earlier savings. Switching preserves entered
amounts, date, and note while clearing any transaction link and due date.
