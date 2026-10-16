# Dashboard and Frontend

[Documentation index](../README.md#documentation)

## Layout and data loading

The dashboard has a hero/chart on the left, a welcome/balance sidebar on the
right, and Accounts and Recent Transactions panes below. It adapts to stacked
layouts on narrower screens. The sidebar stretches on desktop so its final
balance card aligns with the bottom of the trend section.

The shared app layout includes a centered footer with the current year,
copyright text, and `Coiny-Logo-Hrztl.svg`. It stays at the bottom of short pages
and follows content on long pages, wrapping its text/logo on narrow screens.
The header's blue divider is one pixel thick to match the site's light borders.

| Data | Source |
| --- | --- |
| Current net worth, retirement, liquid funds, debt, accounts, recent transactions | `GET /api/dashboard` |
| Hero's selected historical series and change | `GET /api/dashboard/net-worth-trend` |
| First name | `GET /api/auth/me` |
| Household name | `GET /api/households/me` |

Financial summary, selected trend, and personalization load independently. Failed
profile requests fall back to generic text rather than blocking financial data.
The header and dashboard share the current-user request/state through `AuthService`.
Accounts and transactions share the summary request; retrying either reloads it.

## Hero chart

The large net-worth value comes from the current summary and stays independent
of the selected historical range. The adjacent dollar/percentage changes come
from that range's trend result. Null percentages and their parentheses are
hidden; valid zero-percent changes remain visible.

`TrendChart` is a reusable Angular/SVG component with an orange line, subtle
fill, and no persistent axis labels. Hover or tap shows the exact date/value, a
guide, and a highlighted point. Keyboard focus supports arrow keys, Home, End,
and Escape, with an accessible value description. One point displays a dot;
constant histories remain a flat line. A faint zero line appears when values
cross zero. There is no added charting dependency.

The graph has loading, empty-history, and error/retry states. Changing range
cancels a superseded request so an older response cannot replace the selection.

## Date ranges

Controls appear in this order; `1M` is initially selected:

| Control | Inclusive start through today |
| --- | --- |
| `1D` | Yesterday; two daily values |
| `1W` | Seven calendar days ago; eight daily values |
| `1M` | One calendar month ago |
| `3M` | Three calendar months ago |
| `6M` | Six calendar months ago |
| `1Y` | One calendar year ago |
| `YTD` | January 1 of this year |

Frontend calendar dates match the backend's America/New_York timezone. Month/year
subtraction clamps to the last valid day of the destination month. These are daily
balance histories, not intraday charts. Empty-history rules and future-date limits
are defined in [account trends](03-account-trends.md).

## Welcome and balance sidebar

The welcome panel is blue with white text and uses the
[user/household greeting](04-user-profile.md#dashboard-personalization).
Retirement uses the orange/gold accent with blue text; liquid funds use green,
and debt uses red, both with white text. Amounts are drawn from the summary.

## Accounts pane

Rows show an institution initial/avatar, uppercase account name, institution
name, right-aligned balance, and `1M change`. Without an institution, the initial
comes from the account name and the subtitle is “No institution.” Liability
balances include “owed.” All returned accounts are shown in the API's name order.

Monthly trends come from each account's `monthlyTrend` in the dashboard response
and do not change with the hero selection. Asset increases are green and decreases
red. For credit cards and loans, increasing debt is red and decreasing debt green.
The arrow always follows numerical movement. Neutral changes have no arrow;
zero-baseline trends show a dollar change instead of an unavailable percentage.

## Recent Transactions pane

The pane renders the ten most recent transactions returned by the dashboard,
ordered by transaction date descending and then ID descending. Rows show account
and institution, signed amount, description, optional merchant, and calendar date.
Missing descriptions fall back to the category name. Dates retain the stored
ledger day rather than shifting into the browser timezone.

Income uses a green up icon and plus sign; expenses use a red down icon and minus
sign. This represents transaction category, distinct from liability balance
movement in the Accounts pane. The renderer has a neutral transfer fallback,
although the API currently rejects transfer creation.

Both panes alternate avatars by displayed row: orange background/blue initial,
then blue background/orange initial. Each list starts with orange. Avatar color
is decorative and no longer tied to account type. Rows are informational; there
are no dashboard add-account or add-transaction buttons. Those actions belong
on the accounts page.

Both panes have loading, empty, and retry states. Financial colors are reinforced
by signs, arrows, and accessible descriptions. Long labels/amounts wrap, and row
details rearrange for narrower screens.

## Accounts page

`/accounts` defaults to All accounts and 1M. The blue selector offers individual
accounts (including closed accounts labeled as such) and All accounts. A
standalone Add Account button sits opposite the Accounts page heading.
Current balance is independent of the historical range. All accounts
shows net worth (assets minus debt); a credit card or loan shows Amount owed.
The account card includes type/status and an institution website when available.
Only HTTP/HTTPS website links are allowed.

The page reuses the dashboard's trend chart and date ranges. Selecting an account
scopes both history and transactions by account ID. Chart range, inclusive ledger
date bounds, and transaction date range stay aligned. Account/range selections
are reflected in query parameters for reloads and direct links.

The Transactions heading has a filter button opening search, type, and category
controls. Apply combines the filters; Clear removes them. Search matches
description, merchant, category, or account name. Filtering and pagination happen
on the server through `GET /api/transactions/paged`, with ten rows per page.
Previous/next controls and up to five page numbers appear at the bottom, with a
result count. Account, timeframe, and applied filter changes reset to page one.
Superseded requests are cancelled. Loading, empty, and retry states are provided.

Account, filter, and creation-form selectors use the reusable `SelectMenu`
component: a white dropdown surface with the profile menu's border, rounded
corners, shadow, and selected indicator. Arrow keys, Home/End, typeahead,
Enter/Space, Escape, and outside-click dismissal are supported.

Add Account opens a modal form for name, type, optional existing institution,
opening balance, and opening date. It creates the opening snapshot through the
existing API, refreshes the page, and selects the new account. Add Transaction
opens a modal with the current account preselected and excludes unsupported
transfer categories. Saving refreshes balances, trend, and transactions without
leaving the page; timeframe and applied filters are retained.
Creating transactions uses active accounts and active categories.
Creation modals have a dimmed backdrop, Close and Cancel controls, native dialog focus
containment, Escape/outside dismissal, scroll locking, and focus restoration.
Dismissal is disabled while a save is pending.
The secondary outlined Add Category button sits beside Add Transaction. Its
modal accepts a name (up to 100 characters) and Income/Expense type. Categories
belong to the household and are available across accounts. Saving updates the
category filter immediately; the transaction modal loads the latest categories
when opened. Duplicate-name errors stay visible in the form for correction.
Institution creation and account edit/delete are separate future work.

## Income and expense breakdown

A bordered category pane sits beside Transactions on desktop and below it on
narrow screens. It defaults to Expenses, with an Expenses/Income toggle.
It follows the selected account and timeframe, independently of transaction-list
search, category/type filters, and pagination. Saving an account or transaction
refreshes it while preserving its toggle selection.

`GET /api/transactions/category-breakdown` aggregates the full matching ledger
by category, including uncleared entries. Expense/income are category types,
not the direction of a liability balance change. Transfers are excluded.
The center shows Total Expenses or Total Income and the amount for the selected
period. The legend shows category names, transaction counts, signed amounts,
and percentages, with stable colors based on category ID.

Hovering a slice or legend row highlights the same category and gently enlarges
its slice. A tooltip shows category, amount, percentage, and transaction count.
Keyboard focus exposes the same details; Enter/Space or tap/click can pin a
category, and Escape dismisses the details. Reduced motion disables the slice
movement. Slice hit areas remain stationary to avoid hover jitter.
The chart supports complete single-category rings, loading, empty-period, and
error/retry states. Superseded requests are cancelled.

## Current routes and remaining UI work

| Route | Status |
| --- | --- |
| `/login` | Branded login with animated coins, reduced-motion support, pause, password toggle, and validation/error states |
| `/dashboard` | Implemented dashboard in protected layout |
| `/accounts` | Account selection, trends, filtered/paginated transactions, income/expense donut |
| `/accounts/new` | Redirects to accounts with the Add Account modal open |
| `/transactions/new` | Redirects to accounts with the Add Transaction modal open, retaining query parameters |
| `/` | Redirects to dashboard inside the protected layout |
| `/budget` | Planned destination; no route implementation |

The logo returns to the dashboard, the accounts icon opens the accounts page,
and the separate transactions icon has been removed. The budget icon remains a
planned destination. The header's dynamic
initial opens a profile menu with name/email and local logout, including keyboard
navigation and outside-click dismissal. Signup, full expired-session handling,
and household onboarding remain planned. See
[authentication](04-user-profile.md#header-profile-menu-and-local-logout) for the
current boundary.

## Components and styling

- `Dashboard`: loading, summary/profile requests, and hero date selection.
- `Accounts`: account selection, scoped history, filters, and server-side transaction pagination.
- `CategoryBreakdownPanel`: independent income/expense aggregation loading and toggle state.
- `DonutChart`: reusable SVG slices, linked legend, tooltips, and keyboard/touch interaction.
- `CreateAccount`: opening balance/date and optional institution.
- `CreateCategory`: household category name and Income/Expense type.
- `SelectMenu`: shared styled, keyboard-accessible form selector.
- `Modal`: shared native-dialog shell for account, transaction, and category creation.
- `TrendChart`: reusable dated-value plot and interaction.
- `AccountList`: account rows and favorable/unfavorable trend presentation.
- `TransactionList`: recent ledger rows and signed category amounts.
- `Icon`: shared SVG shapes, including the supplied up/down assets.
- `AppHeader`: shared profile state, accessible menu, and local logout.
- `AppFooter`: shared copyright and horizontal logo in the protected app layout.

Reuse the existing CSS tokens in `src/styles/variables.css`. Keep business signs,
calendar rules, and API contracts consistent with the backend. See
[frontend commands](../Coiny.Web/README.md) for build/test entry points.
