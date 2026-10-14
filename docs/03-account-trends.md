# Account Balances, Snapshots, and Trends

## Balance history

`OpeningBalanceDate` is a calendar date. The opening balance represents the
balance at the start of that day; transactions on that day affect its closing
balance. Transactions before the opening date are rejected.

Transactions remain authoritative. `AccountBalanceSnapshot` stores a derived
closing balance, with a unique `(AccountId, SnapshotDate)` index. Account creation
saves the opening-day snapshot. Transaction creation recalculates the affected
day and later stored snapshots from the ledger. Account/transaction changes and
snapshot changes commit in one database transaction.

Snapshots are stored on opening and transaction dates. Unchanged days do not
require additional rows. Backdated transactions update subsequent stored
balances rather than simply changing today's balance.

Assets increase with income and decrease with expenses. Credit cards and loans
store positive balances owed, so expenses increase debt and income-category
credits reduce it. Paired transfers, including movements between checking and
credit-card accounts, are not implemented; transfer transactions are rejected.

## Service responsibilities

- `AccountBalanceCalculator`: shared asset/liability transaction rules.
- `AccountBalanceSnapshotService`: household-scoped closing-balance updates and
  sparse history reads, including the last snapshot before a requested range.
- `AccountTrendService`: account selection, daily carry-forward, and aggregation.
- `TrendService`: starting/ending values, absolute/percentage change, and direction.
- `DashboardService`: requests the household net-worth trend.

`IAccountTrendService.GetTrendAsync` accepts an inclusive start/end date, optional
account IDs, and an aggregation mode. Null IDs select all household accounts; an
empty collection selects none. Duplicate IDs count once. Every explicitly
requested account must belong to the household, or the entire request fails.

`SumBalances` supports individual accounts and selected groups such as debt or
retirement accounts. `NetWorth` subtracts credit-card and loan balances from
assets. For each date, an account contributes zero before its opening date and
its last known closing balance afterward. An overlapping range includes every
requested day, including zero days before the first account opens. A range
entirely before all selected accounts open returns no points.

Empty trends have unavailable starting/ending values and changes. A single-point
trend has zero absolute change. Percent change is `change / abs(start) * 100`,
and is unavailable when the starting value is zero. Values remain unrounded.
Direction describes numerical movement, not whether that movement is desirable.

## Dashboard endpoint

```http
GET /api/dashboard/net-worth-trend?startDate=2026-10-01&endDate=2026-10-14
Authorization: Bearer <token>
```

Both dates are required. Malformed, missing, or reversed dates return HTTP 400.
The endpoint requires authentication and derives the household from the user.
It returns `TrendResult` with `points` (`date`, `value`), `startingValue`,
`endingValue`, `absoluteChange`, `percentageChange`, and `direction`.

This endpoint supplies graph data separately from the existing dashboard summary.
The frontend chart and account-group UI are separate milestones.
