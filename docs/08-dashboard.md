# Dashboard and Frontend

[Documentation index](../README.md#documentation)

## Layout and data loading

The dashboard has a hero/chart on the left, a welcome/balance sidebar on the
right, and Accounts and Recent Transactions panes below. It adapts to stacked
layouts on narrower screens. The sidebar stretches on desktop so its final
balance card aligns with the bottom of the trend section.

| Data | Source |
| --- | --- |
| Current net worth, retirement, liquid funds, debt, accounts, recent transactions | `GET /api/dashboard` |
| Hero's selected historical series and change | `GET /api/dashboard/net-worth-trend` |
| First name | `GET /api/auth/me` |
| Household name | `GET /api/households/me` |

Financial summary, selected trend, and personalization load independently. Failed
profile requests fall back to generic text rather than blocking financial data.
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
on their respective management pages as those pages are built.

Both panes have loading, empty, and retry states. Financial colors are reinforced
by signs, arrows, and accessible descriptions. Long labels/amounts wrap, and row
details rearrange for narrower screens.

## Current routes and remaining UI work

| Route | Status |
| --- | --- |
| `/login` | Basic login form |
| `/dashboard` | Implemented dashboard in protected layout |
| `/transactions/new` | Existing basic creation form; accessible directly |
| `/` | Redirects to dashboard inside the protected layout |
| `/accounts`, `/transactions`, `/budget` | Planned destinations; no route implementations |

The header includes navigation markup for those planned destinations and a static
avatar. A functional profile/logout menu, signup, and household onboarding are
still planned. See [authentication](04-user-profile.md) for the current boundary.

## Components and styling

- `Dashboard`: loading, summary/profile requests, and hero date selection.
- `TrendChart`: reusable dated-value plot and interaction.
- `AccountList`: account rows and favorable/unfavorable trend presentation.
- `TransactionList`: recent ledger rows and signed category amounts.
- `Icon`: shared SVG shapes, including the supplied up/down assets.

Reuse the existing CSS tokens in `src/styles/variables.css`. Keep business signs,
calendar rules, and API contracts consistent with the backend. See
[frontend commands](../Coiny.Web/README.md) for build/test entry points.
