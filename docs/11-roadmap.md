# Remaining product work

## Recurring transactions and subscriptions

Manual scans, daily opt-in household scanning, persistent alerts, and personal
unread badges are implemented; see [recurring detection](14-recurring-detection.md).
Future improvements include recognizing changing bank reference strings and
patterns with missing cycles while keeping suggestions reviewable.

## Savings funds and component goals

Account-backed savings funds, component targets/recurring costs/reserves, paycheck
contribution schedules, affordability reviews, account makeup, and budget integration
are implemented; see [savings funds](19-savings-funds.md). Multiple-account funding
remains deliberately outside the first version.

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
- Password recovery/change, offline recovery codes, revocable sessions, and login protection are implemented; see [account security](20-account-security.md). SMTP and production key/proxy configuration remain deployment tasks.
- Tested backup/restore and data export.
- Merchant/category rules and saved CSV column mappings are implemented; safe import undo/correction is also implemented.

Statement checkpoints and account reconciliation are implemented; see
[account reconciliation](12-account-reconciliation.md).

## Agreed V1 completion scope

Savings funds and the account-security implementation are complete. Remaining V1
feature work is tested backup, restore, and export. Recovery email needs SMTP
configuration and deployment validation. Other refinements above are post-V1.
After these, focus on
infrastructure, container self-hosting (including an explicit server recovery command), and hosted deployment. Per-household databases
and client-side encryption/shared household keys are architecture ideas to evaluate
before hosting; no zero-knowledge privacy claim is currently implemented.
