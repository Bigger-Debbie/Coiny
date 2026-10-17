# API and Postman Reference

[Documentation index](../README.md#documentation)

## Conventions

The default local API base is `http://localhost:5298/api`. Request/response fields
use camelCase. Send JSON creation bodies with `Content-Type: application/json`.
Use named enum strings from the tables below. Dates in examples are illustrative;
choose appropriate historical dates for your test account.

All endpoints except registration and login require:

```http
Authorization: Bearer <token>
```

Financial services derive the current household from the authenticated user.
Create a household before calling financial endpoints for a newly registered user.
The current app supports one household per user through creation or invitation redemption.

## Endpoint inventory

Paths below include `/api`; in Postman, append the part after `/api` to `{{baseUrl}}`.

| Method | Path | Successful response | Purpose |
| --- | --- | --- | --- |
| POST | `/api/auth/register` | 200 | Register and issue token |
| POST | `/api/auth/login` | 200 | Sign in and issue token |
| GET | `/api/auth/me` | 200 | Current user profile |
| GET | `/api/households/me` | 200 | Current household and role; 404 if none |
| POST | `/api/households` | 201 | Create household and owner membership |
| GET | `/api/households/members` | 200 | Current household member cards and caller flag |
| DELETE | `/api/households/members/{membershipId}` | 204 | Owner removes a regular member and revokes active join code |
| GET | `/api/households/invitations` | 200 | Owner invitation status |
| POST | `/api/households/invitations` | 200 | Owner generates/replaces a join code |
| DELETE | `/api/households/invitations` | 204 | Owner revokes a join code |
| POST | `/api/households/invitations/preview` | 200 | Preview target household without joining |
| POST | `/api/households/invitations/redeem` | 200 | Confirm and join as a member |
| GET | `/api/institutions` | 200 | Household institutions |
| POST | `/api/institutions` | 201 | Create institution |
| GET | `/api/accounts` | 200 | Accounts, opening dates, and current balances |
| POST | `/api/accounts` | 201 | Create account and opening-day snapshot |
| GET | `/api/accounts/{accountId}/trend` | 200 | Daily balance trend for one owned account |
| GET | `/api/categories` | 200 | Household categories |
| POST | `/api/categories` | 201 | Create category |
| GET | `/api/transactions` | 200 | Household transactions, newest first |
| GET | `/api/transactions/paged` | 200 | Filtered transaction page with total count |
| GET | `/api/transactions/category-breakdown` | 200 | Income or expense totals grouped by category |
| POST | `/api/transactions` | 201 | Create transaction and update snapshots |
| POST | `/api/transaction-imports/validate` | 200 | Check included rows, categories, duplicates, and projected balance without writing |
| POST | `/api/transaction-imports` | 200 | Import a reviewed batch atomically or replay its result |
| GET | `/api/transaction-imports` | 200 | Last 50 household/account import batches |
| POST | `/api/transfers` | 201 | Create both sides of a manual transfer atomically |
| POST | `/api/transfers/entries` | 201 | Record one unlinked or external transfer entry |
| POST | `/api/transfers/links` | 200 | Link two existing transfer entries without changing balances |
| DELETE | `/api/transfers/links/{transactionId}` | 204 | Unlink both sides, preserving entries and balances |
| GET | `/api/dashboard` | 200 | Totals, accounts with monthly trends, ten recent transactions |
| GET | `/api/dashboard/net-worth-trend` | 200 | Daily net-worth series for the requested range |
| GET | `/api/budgets/setup` | 200 | Budget setup status and resumable draft |
| GET | `/api/budgets/draft` | 200 or 204 | Saved draft, or no content when absent |
| POST | `/api/budgets/draft` | 200 | Start or resume the household draft |
| PUT | `/api/budgets/draft/{draftId}` | 200 | Replace draft definitions and save wizard progress |
| GET | `/api/budgets/draft/{draftId}/preview` | 200 | Preview draft for a month and edit version |
| POST | `/api/budgets/draft/{draftId}/publish` | 200 | Publish the reviewed draft |
| GET | `/api/budgets/month` | 200 | Published monthly plan and calendar events |
| GET | `/api/budgets/income-period` | 200 | Current household or individual income period and scheduled bills |

Collection list responses remain JSON arrays. Paged transaction and category
breakdown endpoints return structured objects. Financial records have no public
single-resource detail/update/delete endpoints. Invitation revocation is listed
above. Logout, refresh-token, and snapshot-management endpoints are not exposed.

Household budgeting exposes the [setup, draft, preview, publication, and monthly
projection endpoints](#household-budgeting) below. Calendar events are included in
monthly projections; they describe planned occurrences.

In Development, `/openapi/v1.json` exposes the generated contract. It is outside
the `/api` base and is a JSON document, not a configured Swagger UI.

## Postman workflow

Create environment variables:

| Variable | Example/use |
| --- | --- |
| `baseUrl` | `http://localhost:5298/api` |
| `token` | JWT returned by login/registration |
| `institutionId` | ID returned by institution creation/listing |
| `accountId` | ID returned by account creation/listing |
| `categoryId` | ID returned by category creation/listing |
| `householdId` | Target ID returned by invitation preview |
| `membershipId` | Membership ID from the household roster; not an Identity user ID |
| `joinCode` | Fresh owner-generated invitation code; expires after 10 minutes |
| `draftId` | ID returned by starting/resuming a budget draft |
| `editVersion` | Latest draft edit version returned by start/save/publish |
| `month` | First day of the requested month, e.g. `2026-11-01` |
| `sourceKey` | Optional income source GUID from budget responses |

Use collection-level Bearer Token authorization with `{{token}}`. Set login and
registration requests to **No Auth**. An optional post-response script on login
or registration can save the token:

```js
const body = pm.response.json();
if (pm.response.code === 200 && body.token) {
  pm.environment.set('token', body.token);
}
```

For the [demo database](05-demo-data.md), log in, inspect `/auth/me` and
`/households/me`, then request `/dashboard` and `/dashboard/net-worth-trend`.
Do not register the seeded email again.

For a fresh user: register → create household → optionally create institution →
create accounts and categories → create transactions → inspect dashboard/trends.
Use returned IDs rather than assuming they match the examples.

To join instead: the owner signs in and generates a code with
`POST /households/invitations` (no body). Sign in as the new user, preview the
code, then redeem with the returned household ID after confirming the target.
Use separate owner/new-user Postman environments so their tokens are not mixed up.
The code appears only in the generation response; status cannot retrieve it.

For budgets: start/resume draft → save full definitions → preview with the latest
edit version → publish → fetch month and income period. See the payloads below.
The inline Add Institution and Add Category UI flows use the existing
`POST /institutions` and `POST /categories` endpoints; no additional routes were added.

## Authentication payloads

`POST /api/auth/register`:

```json
{
  "firstName": "Alex",
  "email": "alex@example.test",
  "password": "DemoOnly!2026"
}
```

`firstName` is required, trimmed, and limited to 100 characters. Email must be
valid. Password must be at least eight characters and satisfy Identity's
complexity requirements. Registration returns a token but no household.

`POST /api/auth/login`:

```json
{
  "email": "alex@example.test",
  "password": "DemoOnly!2026"
}
```

Both successful calls return:

```json
{
  "token": "<jwt>",
  "email": "alex@example.test"
}
```

`GET /api/auth/me` returns:

```json
{
  "id": "<identity-user-id>",
  "email": "alex@example.test",
  "firstName": "Alex"
}
```

`firstName` can be null for an older user. See
[authentication behavior](04-user-profile.md) for token storage and current limits.

## Household and institution payloads

`POST /api/households`:

```json
{
  "name": "Ross"
}
```

Name is required, maximum 100 characters. Creation establishes an `Owner`
membership and returns 409 for a user who already belongs to a household.
`GET /api/households/me` returns `id`, `name`, and `role` (`Owner` or `Member`).

`POST /api/institutions`:

```json
{
  "name": "Demo Community Bank",
  "website": "https://example.com"
}
```

Name is required, maximum 100 characters; website is optional, maximum 250.
Institution responses contain `id`, `name`, `website`, and `isActive`.

## Account payload

`POST /api/accounts`:

```json
{
  "name": "Primary Checking",
  "accountType": "Checking",
  "institutionId": 1,
  "openingBalance": 1000.00,
  "openingBalanceDate": "2026-04-01"
}
```

Name is required, maximum 100 characters. `institutionId` is optional/null; if
supplied, it must belong to the household. Opening balance must be nonnegative;
opening date is required and represents the start of that calendar day.

Account types: `Checking`, `Savings`, `CreditCard`, `Investment`, `Retirement`,
`Loan`, `Cash`, `Crypto`, `Other`.

Create/list responses include `id`, `name`, `accountType`, `openingBalance`,
`openingBalanceDate`, `currentBalance`, `isActive`, `institutionId`, and
`institutionName`. Institution fields can be null. These endpoints do **not**
include monthly trends; use `/dashboard` for the account pane's data.

## Category payload

`POST /api/categories`:

```json
{
  "name": "Groceries",
  "categoryType": "Expense"
}
```

Name is required; the service rejects a duplicate name in the household
case-insensitively. Responses contain `id`, `name`, `categoryType`, `isSystem`,
and `isActive`.

Types are `Income`, `Expense`, and `Transfer`. Use the dedicated transfer endpoints
for Transfer entries; ordinary transaction creation rejects Transfer categories.

## Transaction imports

All routes require authentication and current household membership. Apply
`AddTransactionImportBatches` first. These endpoints accept normalized reviewed
JSON rows, not raw CSV files. One active account per batch; 1–2,000 included rows.
Preview checks and import both use this shape:

```json
{
  "accountId": 1,
  "submissionId": "f501388c-9da7-4e29-93d1-34c74c3336a1",
  "filename": "bank-export.csv",
  "rows": [
    {
      "rowId": 2,
      "date": "2026-10-16",
      "amount": 25.50,
      "direction": "Out",
      "categoryType": "Expense",
      "categoryId": 1,
      "counterparty": "Unknown",
      "description": "Weekly groceries",
      "merchant": "Example Market",
      "notes": null,
      "isCleared": true,
      "allowDuplicate": false
    }
  ]
}
```

Use a fresh nonempty GUID for a new reviewed submission. Row IDs must be unique
positive integers (the UI uses physical CSV line numbers). Filename is required,
up to 255 characters. Dates may not precede the account's opening date; future dates
follow the existing ledger behavior and remain accepted. Amount is positive with
at most two decimals. Description is required up to 250; optional merchant/notes
are limited to 150/1000. Omit excluded rows entirely.

Income requires direction In and a household Income category; Expense requires Out
and an Expense category. All categories must be active. Transfer accepts In/Out
and Unknown/External counterparties, with categoryId omitted/null or an active
household Transfer category. The transfer category is resolved automatically when
recording the row; no paired entry is created. Linked is rejected on imported rows.

`POST /api/transaction-imports/validate` returns `rows` (rowId, errors,
duplicateTransactionIds, duplicateRowIds), `canImport`, `currentBalance`, and
nullable `projectedBalance`. This creates no records. Duplicates match account,
calendar date, amount, money direction, and normalized case/whitespace description.
Existing and within-batch matches require explicit allowDuplicate=true or removal
from the batch. Up to five matching IDs per type are returned as examples.

`POST /api/transaction-imports` revalidates inside a database transaction. On success
it returns batchId, submissionId, accountId, filename, importedCount, completedAt
(UTC), and balanceAfter. The batch, every transaction, and all affected daily
snapshots are saved together. Later stored snapshots are recalculated as well.
Repeating the same submissionId and normalized payload returns the original result
without creating more transactions. Different data with that ID returns 409.

`GET /api/transaction-imports?accountId=1` returns the last 50 matching batch responses,
newest batch first. Omit accountId for the current household. Foreign account IDs
return 400. Import history does not expose other households' batches.

Responses: 400 malformed payload/invalid account; 401 unauthenticated; 403 no
household membership; 409 conflicting submission reuse; 422 validly bound rows
that fail business validation at commit. The 422 body is the validation response
with updated row errors; no rows are saved. Unexpected storage failures roll back
and can be retried using the same submission ID. See [import workflow](10-csv-transaction-import.md).

## Account transfers

These endpoints require authentication and current household membership. Apply
`AddAccountTransfers` first. Transfer amounts are positive, with at most two decimal
places. Dates cannot precede the affected accounts' opening dates. As with ordinary
transactions, future dates are currently accepted. Accounts must be active and owned
by the current household. A household Transfer category is reused or created automatically.

`POST /api/transfers` creates two linked entries and their snapshots in one database
transaction. Both entries initially use the same date and cleared status:

```json
{
  "fromAccountId": 1,
  "toAccountId": 2,
  "amount": 500.00,
  "date": "2026-10-16",
  "description": "Move money to spending",
  "notes": null,
  "isCleared": true
}
```

Description is optional (defaults to Transfer), maximum 250 characters; notes are
optional, maximum 1000. From and To must differ. Returns `outgoing` and `incoming`,
each using the transaction response shape.

`POST /api/transfers/entries` records only one account's bank entry, for future CSV
imports or external movements. It does not create an entry in any other account:

```json
{
  "accountId": 1,
  "amount": 500.00,
  "date": "2026-10-16",
  "direction": "Out",
  "counterparty": "Unknown",
  "description": "Funds Transfer",
  "notes": null,
  "isCleared": true
}
```

Direction must be In or Out. Counterparty is Unknown (default) or External;
Linked cannot be supplied here. Description is required. Returns one transaction.

`POST /api/transfers/links` links existing entries:

```json
{
  "firstTransactionId": 101,
  "secondTransactionId": 102
}
```

Both must be unlinked transfers in the current household, on different accounts,
with equal amounts and opposite directions. Posting dates may differ and are
preserved. An explicitly external entry can be linked when the user later tracks
the other account. Returns outgoing/incoming. No entries, balance effects, or
snapshots are added by linking. No automatic matching is performed.

`DELETE /api/transfers/links/101` unlinks both entries, sets each counterparty to
Unknown, and preserves all amounts, dates, cleared flags, and snapshots. This
does not undo or delete a transfer's financial effects.

List, paged, dashboard recent-transaction, and transfer responses expose nullable
`transferDirection` (In/Out), `transferCounterparty` (Unknown/External/Linked),
`pairedTransactionId`, `counterpartyAccountId`, and `counterpartyAccountName`.
Responses also include `accountType` and nullable `counterpartyAccountType`, using
the account enum strings. These support Contribution/Payment display labels without
changing the Transfer classification. Account type is populated for ordinary entries
too; counterparty account type is null when no pair exists.
These are null on ordinary income/expense entries. Unlinked transfers have null
pair/account fields. Transfers stay outside Income/Expense category breakdowns.
Increases/decreases in liability accounts use positive amounts owed: a transfer
into a credit card reduces debt; the corresponding transfer out of checking reduces cash.

Errors: 400 invalid accounts, dates, amounts, directions, or incompatible pairs;
401 unauthenticated; 403 no household membership; 404 missing/foreign transfer
entry; 409 already-linked/not-linked entries or a concurrent link change.
Domain errors use Problem Details; malformed bodies use validation responses.
The Add Transaction modal supports manual paired, unknown, and external entries.
Link/unlink APIs are implemented; the matching UI and CSV import follow on the
feature branch.

## Transaction payload

`POST /api/transactions`:

```json
{
  "accountId": 1,
  "categoryId": 1,
  "amount": 92.35,
  "transactionDate": "2026-10-15",
  "description": "Weekly groceries",
  "merchant": "Demo Market",
  "notes": "Postman example",
  "isCleared": true
}
```

Use actual IDs from the selected household. Both account and category must belong
to it. The amount must be at least 0.01; do not send a negative expense. The
category determines the financial effect. Description is required, maximum 250
characters; merchant/notes are optional with maximum lengths 150/1000.

The date cannot precede the account's opening date. Backdated entries update
later stored snapshots atomically. Future entries are currently accepted;
see the [current-balance distinction](03-account-trends.md#dates-and-calculations).

Create/list responses contain `id`, `accountId`, `categoryId`, `institutionName`, `accountName`,
`categoryName`, `categoryType`, `amount`, `transactionDate`, `description`,
`merchant`, and `isCleared`. Institution name is resolved for both creation and
listing. Notes are stored but are
not included in these response DTOs. Listing orders by date descending, then ID
descending.

### Filtered transaction pages

```http
GET {{baseUrl}}/transactions/paged?accountId=1&startDate=2026-10-01&endDate=2026-10-15&categoryType=Expense&page=1&pageSize=10
```

All filters are optional: `accountId`, `categoryId`, `categoryType` (Income,
Expense, Transfer), inclusive `startDate`/`endDate`, and `search` (up to 100
characters). Search matches description, merchant, account name, or category
name, ignoring case for ordinary English text. Filters combine with AND.
Omit accountId for the whole household. Account/category IDs outside the current
household are rejected with 400.

`page` defaults to 1; `pageSize` defaults to 10 and accepts 1–100. Results retain
date-descending, ID-descending order. The response contains `items` (transaction
DTOs), `totalCount`, `page`, and `pageSize`. Pages past the end clamp to the last
page. Empty results return page 1, totalCount 0, and an empty items array.

### Income/expense category breakdown

```http
GET {{baseUrl}}/transactions/category-breakdown?accountId=1&startDate=2026-10-01&endDate=2026-10-15&categoryType=Expense
```

Requires inclusive `startDate`/`endDate` and `categoryType` (Income or Expense).
Omit `accountId` for the whole household. Reversed/missing dates, Transfer or
unsupported types, and foreign account IDs return 400. Date bounds use ledger
calendar dates, including all transactions on the ending day.

Response fields are `categoryType`, `totalAmount`, `transactionCount`, and
`categories`. Each category includes `categoryId`, `categoryName`, `amount`,
`transactionCount`, and `percentage` (0–100). Groups sort by amount descending,
then name and ID. Amounts are aggregated using decimal arithmetic; percentages
are unrounded in the API and formatted for display by the frontend.
Empty results return zero total/count and an empty categories array.

Example response:

```json
{
  "categoryType": "Expense",
  "totalAmount": 200.00,
  "transactionCount": 5,
  "categories": [
    {
      "categoryId": 1,
      "categoryName": "Groceries",
      "amount": 150.00,
      "transactionCount": 3,
      "percentage": 75
    },
    {
      "categoryId": 2,
      "categoryName": "Restaurants",
      "amount": 50.00,
      "transactionCount": 2,
      "percentage": 25
    }
  ]
}
```

The summary includes the entire selected ledger scope, including uncleared
transactions. It does not accept list search/category filters or pagination.
It groups by the transaction category type regardless of account type.
The UI requests a range ending today in America/New_York. This ledger summary
uses the supplied date bounds; it does not apply the trend engine's future-date
cap. No database migration is needed for this endpoint.

### Individual account trend

```http
GET {{baseUrl}}/accounts/{{accountId}}/trend?startDate=2026-10-01&endDate=2026-10-15
```

Both dates are required. The response is the same TrendResult shape as the
net-worth endpoint below. The account must belong to the current household.
Credit cards and loans show their amount owed; their sign is not reversed.
Future starts and reversed ranges are rejected; future ends are capped at today
in America/New_York, using the existing trend service.

## Dashboard summary

`GET /api/dashboard` returns three top-level fields:

| Field | Content |
| --- | --- |
| `netWorth` | `currentValue`, `retirement`, `liquidFunds`, `debt` |
| `accounts` | All household accounts in name order, with `monthlyTrend` |
| `recentTransactions` | Ten newest transactions, ordered by date then ID descending |

An illustrative account entry:

```json
{
  "id": 1,
  "name": "Primary Checking",
  "accountType": "Checking",
  "currentBalance": 1200.00,
  "institutionName": "Demo Community Bank",
  "monthlyTrend": {
    "startDate": "2026-09-15",
    "endDate": "2026-10-15",
    "absoluteChange": 200.00,
    "percentageChange": 20.0,
    "direction": "Up"
  }
}
```

The account trend period is fixed at one calendar month through today. Changes
can be null when no history exists; percentage can also be null for a zero
starting balance. Direction is `Up`, `Down`, or `Neutral`.

Recent transaction entries contain `id`, `accountName`, `institutionName`,
`categoryName`, `categoryType`, `amount`, `transactionDate`, `description`, and
`merchant`. The dashboard DTO does not include `isCleared` or notes.

## Net-worth trend

```http
GET {{baseUrl}}/dashboard/net-worth-trend?startDate=2026-10-14&endDate=2026-10-15
```

Both query dates are required. Missing/malformed/reversed dates or a future
start return 400. A future end is capped at today in America/New_York. Dates are
inclusive. An illustrative two-day result:

```json
{
  "points": [
    { "date": "2026-10-14", "value": 1000.00 },
    { "date": "2026-10-15", "value": 950.00 }
  ],
  "startingValue": 1000.00,
  "endingValue": 950.00,
  "absoluteChange": -50.00,
  "percentageChange": -5.0,
  "direction": "Down"
}
```

An empty household/range can return an empty `points` array with null values and
changes, and neutral direction. Full calculation and carry-forward rules are in
[account trends](03-account-trends.md).

## Errors and current boundaries

- Invalid credentials return 401. Protected endpoints require a valid bearer token.
- An authenticated `/auth/me` request whose user no longer exists returns 401.
- No current household returns 404 from `/households/me`.
- DTO validation and caught business-rule failures return 400. Payloads vary
  between validation details, Identity errors, and plain messages; there is no
  unified application error envelope yet.
- Some GET financial endpoints do not translate a missing-household exception
  into a friendly response. Complete new-user setup before calling them.
- Created responses currently point to collection actions, not implemented
  single-resource GET endpoints.

These are current contracts, not promises of update/delete
or complete authentication workflows. Planned capabilities are tracked in the
[root README](../README.md#current-status).


## Household budgeting

All routes require a bearer token and current household membership. Household IDs
are resolved from the authenticated user; clients do not supply them. Apply the
pending budget migrations with the [database setup command](06-development-setup.md#restore-and-initialize)
before testing these endpoints. No sample budget is seeded.

| Method | Route | Success | Purpose |
| --- | --- | --- | --- |
| GET | `/api/budgets/setup` | 200 | Household setup status and resumable draft |
| GET | `/api/budgets/draft` | 200 or 204 | Full saved draft, or no content when absent |
| POST | `/api/budgets/draft` | 200 | Start or resume the household draft |
| PUT | `/api/budgets/draft/{draftId}` | 200 | Replace all draft definitions and save the wizard step |
| GET | `/api/budgets/draft/{draftId}/preview?month=2026-11-01&expectedEditVersion=1` | 200 | Project a saved draft without publication |
| POST | `/api/budgets/draft/{draftId}/publish` | 200 | Validate and publish the reviewed draft |
| GET | `/api/budgets/month?month=2026-11-01` | 200 | Published monthly plan and scheduled events |
| GET | `/api/budgets/income-period?asOf=2026-10-16` | 200 | Current income period, schedule choices, bills, and contributions |

Start with `{}` for the current America/New_York month, or:

```json
{
  "effectiveFromMonth": "2026-11-01"
}
```

Starting again resumes an existing draft unchanged, even if a different month was
requested. A new draft copies the applicable published plan, if any.

The setup response contains `currentMonth`, `hasPublishedBudget`,
`firstPublishedMonth`, `currentRevisionId`, and `draft` (full revision or null).
A future-only published plan has `hasPublishedBudget: true` and a null
`currentRevisionId`; setup is complete even though the plan is not effective yet.
A saved draft can coexist with a published plan. Setup reads create no records.

Save the full draft, including entries from other steps:

```json
{
  "expectedEditVersion": 0,
  "effectiveFromMonth": "2026-11-01",
  "setupStep": "Review",
  "incomeSources": [
    {
      "name": "Salary",
      "amount": 2000,
      "schedule": {
        "frequency": "EveryTwoWeeks",
        "startDate": "2026-10-02"
      }
    }
  ],
  "allocations": [
    {
      "name": "Rent",
      "amount": 1500,
      "allocationType": "Housing",
      "schedule": {
        "frequency": "Monthly",
        "startDate": "2026-11-01",
        "dayOfMonth": 15
      }
    },
    {
      "name": "Groceries",
      "amount": 600,
      "allocationType": "Spending"
    }
  ]
}
```

All five top-level save fields are required. Use explicit empty arrays for
unfinished steps; omitted arrays are rejected rather than clearing saved work.
Income entries require name, amount, and schedule; allocations require name,
amount, and allocation type. Schedules require frequency and start date.
The [budget rules](09-household-budgeting.md) define allowed enums, recurrence
fields, amount precision, and publication requirements.

Start/save/publish return the complete revision: `id`, `revisionNumber`,
`editVersion`, `effectiveFromMonth`, `state`, `setupStep`, `publishedAt`,
`incomeSources`, and `allocations`. Save and publication increment `editVersion`;
use the latest returned value on the next request. Publish with:

```json
{
  "expectedEditVersion": 1
}
```

Preview requires both query parameters and a month on or after the draft's
effective month. It returns `draftId`, `editVersion`, and `month`, containing the
same projection shape as the published month endpoint. Preview is read-only and
can show an incomplete draft; publication performs the completeness checks.

Monthly results include `month`, `revisionId`, `expectedIncome`, `allocated`,
`unassigned`, `overBudget`, `incomeSources`, `allocations`, and `events`.
Each monthly item has ID, name, allocation type (null for income), planned amount,
and occurrence count. Events have date, item ID/name, income flag, allocation type,
and per-occurrence amount. Without an applicable published revision, the published
month endpoint returns a null revision ID and empty, zero-valued results.
Draft previews never change these published results or create transactions.

Invalid dates, amounts, schedules, incomplete publication, missing required fields,
and malformed queries return 400. Month dates must be the first day of a month.
Missing authentication returns 401; an authenticated user without household
membership receives 403. A missing, foreign, or already-published draft ID returns
404. Stale edit versions return 409: reload the draft and reconcile changes before
retrying. Domain errors use Problem Details; binding errors use Validation Problem
Details. Over-budget plans are allowed and report the excess separately.


### Optional paycheck details and extended frequencies

Income input/response objects also accept nullable `healthInsurancePerPaycheck`
and `retirementContributionPerPaycheck`. Omit or leave null to skip tracking.
These are already deducted from take-home pay, recorded per scheduled paycheck;
they do not change available income, allocations, or unassigned money. Negative
amounts or more than two decimal places are rejected. Tax withholding is not tracked.
Monthly responses and income monthly items expose `healthInsurance` and
`retirementContributions`, calculated using that month's actual payday count.

`EverySixMonths` and `Yearly` are additional schedule frequencies. Both use
`startDate` as their anchor and require `dayOfMonth`/`secondDayOfMonth` to be
absent or null. They accept the same optional inclusive `endDate`. The original
anchor day is clamped for short months without changing later occurrences.
A full six-month/yearly payment is included only in its due month, not amortized.
Existing endpoint paths and required fields are unchanged.


### Current income period

`GET /api/budgets/income-period` returns the current household income period.
Optional queries: `asOf=2026-10-16` (defaults to today in America/New_York) and
`sourceKey=<guid>` (omitted for household income). The selected source must exist
in the household's applicable published plan for that date; foreign/unknown or
malformed keys and invalid dates return 400. Authentication and household access
use the same 401/403 behavior as other budget endpoints.

Response fields: `asOf`, `sourceKey`, `sources` (sourceKey/name choices),
`startDate`, `endDate`, `nextPayday`, `billsTotal`, `contributionsTotal`, `bills`,
and `contributions`. The event arrays have the monthly scheduled-event shape.
Income monthly items/events now also expose `sourceKey` (null for allocations).

The latest distinct payday on/before `asOf` starts the period; the next payday
ends it exclusively. Bills on the next payday belong to the next period. Housing
and Bills contribute to `billsTotal`; Savings and Investments are separate.
All household bills are included even with an individual income schedule.
Unscheduled monthly allocations create no dated events. Lookup uses published
revisions over twelve calendar months on each side, covering yearly schedules.
If no prior or next payday is known, dates are nullable and event lists/totals
are empty; clients must not infer an open-ended period or payment status.

Income input/response objects include optional nullable `sourceKey` GUIDs. Retain
returned keys on edits to preserve a source across revisions; omit for new sources.
Empty/duplicate keys within one draft are rejected. Existing historical rows get
unique keys on migration; no identity is inferred from matching names.


## Household invitations

All routes require a bearer token. One user can belong to one household. Create
household returns 409 when a membership already exists (including concurrent
create/join attempts). Invitations add users as Member; only Owners manage codes.

| Method | Route | Success | Behavior |
| --- | --- | --- | --- |
| GET | `/api/households/invitations` | 200 | Owner-only active status (`hasActiveInvitation`, nullable `expiresAt`); no plaintext code |
| POST | `/api/households/invitations` | 200 | Owner-only generation/rotation; returns `code`, `expiresAt`, `householdName` |
| DELETE | `/api/households/invitations` | 204 | Owner-only revocation; idempotent |
| POST | `/api/households/invitations/preview` | 200 | Verify code and return `householdId`, `householdName`, `expiresAt`, without joining |
| POST | `/api/households/invitations/redeem` | 200 | Explicitly join the previewed household as Member; returns HouseholdResponse |

Preview body:

```json
{
  "code": "K7MP-4X9Q"
}
```

After the user confirms the previewed household, redeem:

```json
{
  "code": "K7MP-4X9Q",
  "householdId": 1
}
```

The household ID must match the code's target. Codes are case-insensitive; spaces
and hyphens are ignored. They contain eight random characters excluding I/O/0/1,
are stored as hashes, expire after 10 minutes, and are single-use. Generation
invalidates the previous code. Preview does not reserve or consume it, so expiry,
revocation, and single-use state are rechecked during redemption. Membership and
consumption are saved in one transaction. Already-member users cannot preview or
redeem another invitation. No account, transaction, or budget is created by joining.

Responses: 400 malformed/missing fields; 401 unauthenticated; 403 invitation
management without Owner membership; 404 invalid/expired/revoked/used code or
mismatched target; 409 existing household membership; 429 excessive requests.
Domain errors use Problem Details. Preview and redeem share 5 attempts/minute per
user; generation allows 5/minute per user; invitation routes also allow 60/minute
per IP. Rate-limit responses include Retry-After. Limits are process local.
Invitation responses use no-store cache headers. There is no email/SMS endpoint.

Apply `AddHouseholdJoinInvitations` before using these routes. The migration adds
one invitation row per household and a unique UserId membership index. Existing
multiple memberships must be resolved first; no data is silently deleted. The
signup UI uses the existing register endpoint and then the create/join workflow.


### Household members

`GET /api/households/members` is available to any current household member. It
returns an array of `id` (membership ID), nullable `firstName`, `email`, `role`
(Owner/Member), and `isCurrentUser`. No caller-supplied household ID is accepted;
people from other households are never included. Responses require authentication
and return 403 when the caller has no current membership.

`DELETE /api/households/members/{membershipId}` is owner-only. Use the membership
ID from the roster, not an Identity user ID. Successful removal returns 204.
The target must be a regular Member in the owner's own household. Owners and the
caller cannot be removed (409); foreign/missing memberships return 404; non-owner
callers receive 403; unauthenticated requests receive 401. Permission and target
checks are enforced server-side, independently of visible buttons.

Deletion removes only the membership, preserving the login, accounts,
transactions, budget, and all other household financial records. Any unconsumed
household invitation is revoked in the same transaction, preventing reuse of an
old shared code. If revocation fails, membership deletion rolls back. There is
no new schema migration for these endpoints.
