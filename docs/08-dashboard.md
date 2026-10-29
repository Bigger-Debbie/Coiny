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

Add Account opens a modal form for name, type, optional institution,
opening balance, and opening date. The inline Add Institution option accepts a
name and optional HTTP/HTTPS website, saves it for the household, and selects it
without losing the account draft. Accounts can also be created without an institution.
It creates the opening snapshot through the
existing API, refreshes the page, and selects the new account. Add Transaction
opens a modal with the current account preselected and an Expense/Income/Transfer
selector. Income/Expense filters the categories to the selected type. Saving refreshes balances, trend, and transactions without
leaving the page; timeframe and applied filters are retained.
Creating transactions uses active accounts and active categories.
The transaction form also offers inline Add Category with a name and Income/Expense
type. Saving selects the new category while preserving the transaction draft;
canceling the category editor leaves the transaction unchanged.
Creation modals have a dimmed backdrop, Close and Cancel controls, native dialog focus
containment, Escape/outside dismissal, scroll locking, and focus restoration.
Dismissal is disabled while a save is pending.
The secondary outlined Add Category button sits beside Add Transaction. Its
modal accepts a name (up to 100 characters) and Income/Expense type. Categories
belong to the household and are available across accounts. Saving updates the
category filter immediately; the transaction modal loads the latest categories
when opened. Duplicate-name errors stay visible in the form for correction.
Account edit/delete remains future work.

### Transfers

Selecting Transfer replaces the category and merchant fields with transfer choices.
Between Coiny accounts uses From/To account selectors. The selected account defaults to From. Destinations exclude that account;
the server also rejects self-transfers. A payment to a credit card or loan reduces
the amount owed. Description is optional; amount must be positive with at most two decimal places.

After accounts, amount, and date are entered, the form checks both accounts for
unlinked transfers of the same amount and correct direction within seven days.
Possible matches show account, posting date, description, and amount. Nothing is
selected automatically when matches exist: choose **Link this entry** or explicitly
**Create both sides**. Choosing an existing entry creates only its missing side,
preserving the existing date, details, cleared status, and import information.
The date and cleared checkbox then apply only to the new entry. Its posting date
must be on/after that account's opening date. When creating two new entries, the
date must satisfy both accounts and the cleared checkbox applies to both.

Changes to accounts, amount, or date clear the previous decision and check again.
Failed checks keep saving disabled; stale matches require a fresh review. New-entry
creation, linking, and snapshot updates are atomic. If entries already exist on
both sides, use transaction details to link them without creating another entry.

External and unknown-counterparty modes offer Money in/out and create only the selected
account's entry. Unknown entries can be linked later; no second entry is inferred.
Open an existing transfer’s details and choose **Reconcile transfer** to link recorded entries or explicitly create a missing counterpart.
Failures preserve the draft and show the API's explanation. The modal cannot close
while a transaction or inline category save is pending.

Dashboard and accounts share transaction rows: transfers use neutral colors and the
transfer icon, signed amounts for In/Out, and labels such as Transfer to Spending,
Transfer in · Not linked, or Transfer out · External account. These signs describe
money direction, not the change in a liability's amount owed. Transfers are excluded
from the Income/Expense donut.

Incoming transfers from checking, savings, or cash into Investment/Retirement display
Contribution from [account]. Incoming transfers to CreditCard/Loan display Payment
from [account]. Other linked movements retain Transfer from/to, including movements
between investment/retirement accounts. Unknown or external sources retain Transfer
because their account type is not known. These are display labels, not tax contribution
records, and do not change the underlying Transfer classification or calculations.

## Income and expense breakdown

The Import CSV action opens `/accounts/import`, preserving the selected account.
This lazy-loaded screen reads files locally, supports manually overridden
columns/file formats, including two-digit years, and opens a separate review view.
Back navigation preserves mappings and row edits until a mapping changes. Its
bounded spreadsheet pane uses alternating grays, sticky headers, status colors
with text labels, and click-to-edit cells with typed controls. Users assign categories
or Transfer per row/in bulk, exclude rows, create categories, resolve duplicates,
and explicitly confirm an atomic import. Successful imports show the new balance
and recent batch history. Raw CSV contents are not uploaded; reviewed normalized
rows go to the batch API. Unknown/external transfer entries affect only this account.
See [CSV import](10-csv-transaction-import.md) for supported layouts and remaining work.

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
| `/signup` | Registration, password confirmation, and invitation-link retention |
| `/household-setup` | Authenticated create/join choice and household confirmation |
| `/household` | Compatibility redirect to `/settings` |
| `/settings` | Household, Preferences, and Security on one page |
| `/dashboard` | Implemented dashboard in protected layout |
| `/accounts` | Account selection, trends, filtered/paginated transactions, income/expense donut |
| `/accounts/new` | Redirects to accounts with the Add Account modal open |
| `/transactions/new` | Redirects to accounts with the Add Transaction modal open, retaining query parameters |
| `/` | Redirects to dashboard inside the protected layout |
| `/budget` | Household budget wizard, monthly allocation dashboard, current income-period bills, and calendar |

The logo returns to the dashboard, the accounts icon opens the accounts page,
and the separate transactions icon has been removed. The budget icon opens the
household budgeting page. Its setup workflow and dashboard delivery sequence
are described in [household budgeting](09-household-budgeting.md).
The header's dynamic
initial opens a profile menu with name/email, a Settings page destination,
category/rule/alert dialogs, and logout, including keyboard navigation and outside-click dismissal. Logout
revokes its server session; password change/recovery and session revocation are
implemented. See [account security](20-account-security.md).

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
- `AppHeader`: shared profile state, accessible menu, Settings navigation, and session logout.
- `SettingsPage`: Household, Preferences, and Security cards, with pending-change protection.
- `AppFooter`: shared copyright and horizontal logo in the protected app layout.

Reuse the existing CSS tokens in `src/styles/variables.css`. Keep business signs,
calendar rules, and API contracts consistent with the backend. See
[frontend commands](../Coiny.Web/README.md) for build/test entry points.


## Transaction corrections

The accounts page highlights transaction rows on hover/focus. Clicking a row or
activating its full-row button with Enter/Space opens read-only **Transaction Details**.
It shows the date, amount, account/institution, type/category, merchant, cleared
status, notes, transfer counterparty, import source, and bill link when present.
**Edit transaction** switches the same modal into editing. Cancel returns to details,
with a discard prompt for unsaved changes. The dashboard recent-transactions pane uses the same highlighted rows and details
modal. The lazily loaded editor supports
account, type/category, amount, date, description, merchant, notes, and cleared
status. Loading errors can be retried; stale writes require reloading current
server details. Closing with unsaved changes prompts before discarding, and
refresh/close warns while dirty or saving.

The Delete action opens an explicit confirmation. For a linked transfer it names
the other account and confirms deletion of both entries. Updating a transfer's
amount adjusts both entries; other fields apply only to the selected side.
Unknown/external transfer entries can be corrected to income/expense and vice
versa. Linked transfers keep their type/direction and distinct accounts.

Bill-linked transactions retain their link/payment status on edits. They cannot
be changed from Expense until unlinked in the bill payment dialog. Deleting one
detaches its bill while retaining payment status. Imported entries retain batch
provenance; batch counts describe the original import and are not rewritten.

After save/delete, accounts, trends, filtered transactions, and category totals
reload. The selected account, range, and filter values remain; the list returns
to its first page. Account/category editing and archiving remain future work.


Dashboard account rows highlight on hover/focus and link to `/accounts?accountId=ID`,
so keyboard activation and opening in a new tab retain the selected account.
Dashboard transaction rows open the shared details modal in place. Changes through
that modal reload dashboard balances, account trends, recent transactions, and
the hero chart while retaining the selected chart range.


## Mortgage and personal settings

The home balance sidebar shows Retirement, Liquid Funds, Other Debt, and—only
when the household has an explicitly classified mortgage account—Mortgage.
Mortgage uses the primary blue background with white text. Other Debt excludes
mortgages; the mortgage pill remains visible even when omitted from the headline.
A zero-balance mortgage still counts as an existing mortgage account.

The profile menu's **Settings** item opens the Settings page. Its **Preferences**
section includes **Include mortgage in net worth**. It defaults on and is saved per authenticated user. Turning it off labels
the headline **Net Worth**, followed by a smaller gray **excluding mortgage** label. Saving updates the cached profile; the dashboard uses the preference when opened. The current value, all daily
points, and dollar/percentage changes use the same preference. **Reset changes** discards edits without changing the saved view. Shared account balances, transaction history, and bill
schedules are unaffected. The accounts page's All accounts view follows the same
preference; individual loan balances/trends continue to show the actual debt.

New Loan accounts offer Mortgage, Auto, Personal, Student, and Other. For an
existing loan, select it on the accounts page and choose **Change** beside Loan
Type in the balance card. Existing loans default to Other until explicitly
classified. Changing a subtype changes classification across the displayed
history, without recalculating individual ledger balances. Property/home values
are not inferred or added automatically.

The Accounts pane shows a neutral dash when a percentage change cannot be calculated from a zero starting balance. Its tooltip explains why; a dollar change is not substituted into the percentage column.

### Action notifications

Action confirmations appear as shared toast notifications in the lower-right corner across routes, including account and transaction creation, updates/deletion, CSV imports, budget saves/publication, bill payment updates, household actions, and preference saves. Each notification remains for 30 seconds, stacks independently, and has a dismiss button. Success uses a green accent and errors use red, with text and icons as additional cues. Notifications use the site’s surface, border radius, typography, and shadow tokens. A nonmodal popover keeps feedback visible above dialogs. Error details needed to correct a form remain beside that form as well.

### Edit account details

Select an individual account on the Accounts page and choose **Edit Account** beside **Add Account**. The modal supports name, institution (including on-the-fly institution creation), loan subtype, and opening balance/date corrections. Account type stays fixed. Saving refreshes the selected account’s balances, trend, and transactions and shows a success toast. Opening balance corrections recalculate history; an opening date after an existing transaction is rejected.

The dashboard shows a blue **Investments** total between Welcome and Retirement whenever the household has an Investment account, even if its balance is zero. It sums Investment accounts separately from Retirement and Liquid Funds. Net worth already includes these balances and is unchanged by this display.

### Reconcile existing transfer entries

From an unlinked transfer’s details (Accounts or the home dashboard), choose **Reconcile transfer**, select the other account, and review same-amount/opposite-direction candidates. Date windows are same day, ±7 days, ±30 days, or ±1 year. Same-day candidates appear first; none is automatically selected, including a unique match. Candidates show account, posting date, description, amount, and import batch when present. Multiple matches require the user to choose the correct entry.

**Link existing entries** records only the relationship, preserving both balances, dates, transaction fields, import history, and snapshots. If only one side exists, **The other entry hasn’t been recorded — create it** opens a distinct confirmation with posting date, balance-impact explanation, existing-match warning, and a required acknowledgment. It adds exactly one counterpart, never a replacement pair. Closed accounts permit matching but not new counterparts. Changed entries require reloading before retrying.

Linked transfer details offer **Unlink entries (keep both)** with confirmation; both ledger entries and balances stay intact. This differs from deleting a linked transfer, which still removes both entries. Reconciliation does not automatically repair duplicates from earlier workflows or automatically repair previously duplicated movements. Multi-account CSV sessions are available through Import CSV.

The Accounts **Import CSV** action opens the four-step multi-account session: Files → Review each file → Match transfers → Confirm. Single-file imports use the same flow; file-switching controls are hidden for one file and transfer matching is skipped when there are no suggestions. See [CSV import](10-csv-transaction-import.md) for review and retry behavior.

## Page loading transition

Authenticated pages briefly display a solid white overlay below the header
with the full-color `favicon.svg` coin rotating around its vertical axis. It remains for about 600
milliseconds on fast loads, waits for navigation and initial API requests to settle,
and fades out over 450 milliseconds to reveal the page. Page content is inert while covered; the header remains available.
Later searches, saves, and background polling do not start another overlay.
Canceled/failed navigation dismisses it, and an eight-second safety deadline
prevents a stalled request from trapping the user. Late responses from an older
route cannot delay or dismiss the new route's loader. Reduced-motion preferences
disable coin rotation and the fade animation.
