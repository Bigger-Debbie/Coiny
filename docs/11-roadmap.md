# Remaining product work

## Recurring transactions and subscriptions

Detect likely recurring expenses from repeated merchants, similar amounts, and weekly/monthly/yearly patterns. Compare suggestions with the household budget and flag possible subscriptions or recurring bills that are not accounted for. Let users add a budget item, link an existing bill, or dismiss a suggestion. Allow variable utility amounts. Suggestions require user review; do not automatically create bills or double-count planned spending.

## Savings funds and component goals

After the income-period budgeting view, add savings funds that carry balances
forward and optionally group several goals (for example car repairs, annual tires,
and six-month insurance) under one monthly contribution. Track opening fund
balances, contributions, withdrawals, scheduled bills funded from a fund, and
projected shortfalls at due dates. Fund-backed payments must not duplicate the
pay-period funding allocation. Keep this separate from the initial income-period
budgeting change.

## Other planned areas

- Account-balance forecasting and explicit paycheck/source matching. The zero-based
  running remainder across complete household income periods is implemented for
  this period and 1/3/6/12 months, carrying surpluses and shortfalls forward.
  Allocation-detail forecasts, comparisons, category defaults, and individual
  assignments are also implemented.
- Safe import undo/correction, accounting for edits, transfer links, and bill links.
- Additional budget-maintenance refinements; direct item editing with effective dates is implemented.
- Password recovery, password changes, session handling, and login protection.
- Tested backup/restore and data export.
- Saved merchant/category rules to speed up CSV review.

Statement checkpoints and account reconciliation are implemented; see
[account reconciliation](12-account-reconciliation.md).
