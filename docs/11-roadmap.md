# Remaining product work

## Recurring transactions and subscriptions

Manual scans, daily opt-in household scanning, persistent alerts, and personal
unread badges are implemented; see [recurring detection](14-recurring-detection.md).
Future improvements include recognizing changing bank reference strings and
patterns with missing cycles while keeping suggestions reviewable.

## Savings funds and component goals

After the income-period budgeting view, add savings funds that carry balances
forward and optionally group several goals (for example car repairs, annual tires,
and six-month insurance) under one monthly contribution. Track opening fund
balances, contributions, withdrawals, scheduled bills funded from a fund, and
projected shortfalls at due dates. Fund-backed payments must not duplicate the
pay-period funding allocation. Keep this separate from the initial income-period
budgeting change.

## Other planned areas

- Individual account-balance forecasting and explicit paycheck/source matching. The zero-based
  running remainder across complete household income periods is implemented for
  this period and 1/3/6/12 months, carrying surpluses and shortfalls forward.
  Allocation-detail forecasts, comparisons, category defaults, and individual
  assignments are also implemented. The optional available-cash card now uses a
  selected funding pool of cash accounts and credit cards; per-account future flows
  and explicit paycheck matching remain future work.
- Import review/correction and safe undo are implemented, including transfer/payment
  links, reconciliation invalidation, and audit history. Future work can add bulk
  correction tools and older-history pagination.
- Additional budget-maintenance refinements; direct item editing with effective dates is implemented.
- Password recovery, password changes, session handling, and login protection.
- Tested backup/restore and data export.
- Merchant/category rules and saved CSV column mappings are implemented; safe import undo/correction is also implemented.

Statement checkpoints and account reconciliation are implemented; see
[account reconciliation](12-account-reconciliation.md).
