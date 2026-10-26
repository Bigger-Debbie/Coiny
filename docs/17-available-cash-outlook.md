# Available cash outlook

The income-period remainder card offers two views. **Period surplus** is the default:
actual income and spending so far plus remaining income, spending, savings goals,
and investment contributions for that period. It starts at zero, not at an account balance.

**Available cash** starts with today's recorded balances in selected checking, cash,
and savings accounts, subtracts selected credit-card balances, adds income still
expected, and subtracts allocations still to come through the selected period's end.
A card credit (negative amount owed) increases the result. A negative estimate stays
negative so funding shortages remain visible.

Use the styled selector at the bottom right of the card to switch views.
**Accounts & calculation** opens a modal for the arithmetic and account checkboxes,
keeping the summary cards at their original size. Closing without applying discards
pending checkbox changes. Active
checking accounts and credit cards are initially selected. Savings and cash accounts
are opt-in; emergency and earmarked savings should be excluded unless they fund
this budget. At least one cash account must be selected. Investments, loans, foreign
household accounts, and accounts whose opening date is in the future are excluded.
Archived eligible accounts can be selected explicitly. Apply accounts to recalculate;
reset accounts restores the defaults if a remembered account was removed.

The view and explicit account choices are remembered on this browser, separately
for each signed-in user and household. Storage failure does not prevent use. Neither
view moves money, changes a budget, links payments, or changes ledger balances.
No database migration is needed for this feature.

## Calculation and boundaries

Balances include opening balances and transactions posted through today, excluding
future-dated ledger entries. Card purchases already reduced this net cash position;
card payments move money between cash and debt. Neither is subtracted again as a
posted expense. The cash forecast adds only the change from today's actual running
remainder to the forecast at the selected end date. That preserves existing forecast
rules for future scheduled payments, remaining allowances, savings/investment goals,
and planned income on strictly future paydays. Once a payday arrives, only posted
income counts; a smaller deposit does not imply that the difference is still coming.

For future periods, all intervening income periods are included from today, not just
the final selected period. End dates are limited to the supported twelve-month
forecast horizon. Past periods cannot use today's balances as a historical cash
forecast; choose Period surplus to inspect those periods.

The selected accounts define the funding pool; the remaining spending plan still
covers the whole household. The result assumes those accounts fund future income
and allocations and selected cards are paid off. It is based on recorded data, not a
live bank connection. Account reconciliation, complete imports, and payment links
help keep it meaningful. Unlinked marked-paid items remain reserved, so a posted
expense may temporarily coexist with an outstanding plan estimate until linked.
Transfers across excluded accounts are reflected in today's balances, but this
feature does not predict future discretionary transfers or draw on excluded funds.

For example, $1,615.46 selected cash minus $1,033.15 card debt minus $741.26 remaining
allocations gives **−$158.95 available cash**, even when the zero-based period surplus
is **$488.39**. The two figures answer different questions.

## API

Authenticated read-only calculation: `POST /api/budgets/available-cash` with
`{ "asOf": "2026-10-26", "accountIds": [7, 8, 9, 17, 19] }`.
`asOf` chooses an income period; omitted means today. Omitted/null `accountIds` selects
the defaults; an explicit empty array returns no estimate until cash is selected.
Up to 200 unique household-owned eligible IDs are accepted.

Response: `balanceDate`, `endDate`, `cashBalance`, `cardBalance`, `expectedIncome`,
`remainingAllocations`, nullable `expectedRemaining`, selected `accountIds`, and
eligible `accounts` (id, name, accountType, balance, isActive). The income-period DTO
also includes `householdId` so browser preferences can be isolated by household.

Tests cover carried card debt, previously posted card purchases and income, card
payments, opt-in savings, account scope, missing cash selection, future-period
continuity, route authorization, account-picker behavior, and remembered preferences.
