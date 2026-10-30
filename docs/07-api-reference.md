# API and Postman Reference

## Conventions

The default local API base is `http://localhost:5298/api`. Request/response fields
use camelCase. Send JSON creation bodies with `Content-Type: application/json`.
Use named enum strings from the tables below. Dates in examples are illustrative;
choose appropriate historical dates for your test account.

All endpoints except registration, login, password-recovery requests/options, code recovery, and password resets require:

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
| GET | `/api/auth/recovery-options` | 200 | Installation recovery methods; no account details |
| POST | `/api/auth/recover-with-code` | 200 | Reset password using a saved recovery code |
| GET | `/api/auth/recovery-codes` | 200 | Owned recovery-code count; no plaintext |
| POST | `/api/auth/recovery-codes` | 200 | Verify password, replace set, return codes once |
| POST | `/api/auth/recovery-codes/revoke` | 204 | Verify password and revoke owned codes |
| POST | `/api/auth/forgot-password` | 200 | Request a reset email; generic confirmation |
| POST | `/api/auth/reset-password` | 200 | Redeem single-use reset link |
| POST | `/api/auth/change-password` | 200 | Change password, renew this browser, revoke old sessions |
| GET | `/api/auth/sessions` | 200 | Current user's active sessions |
| DELETE | `/api/auth/sessions/{id}` | 204 | Revoke an owned session |
| POST | `/api/auth/logout` | 204 | Revoke current session |
| POST | `/api/auth/logout-all` | 204 | Verify password and end every session |
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
| GET | `/api/accounts/{accountId}/reconciliations` | 200 | Latest 50 household account statement checkpoints |
| POST | `/api/accounts/{accountId}/reconciliations/check` | 200 | Compare statement and ledger through a date without writing |
| POST | `/api/accounts/{accountId}/reconciliations` | 200 | Confirm reviewed matching statement checkpoint |
| GET | `/api/categories` | 200 | Household categories |
| POST | `/api/categories` | 201 | Create category |
| PUT | `/api/accounts/{id}/loan-subtype` | 204 | Classify an existing household loan |
| GET | `/api/users/me/preferences` | 200 | Current user’s saved dashboard preferences |
| PUT | `/api/users/me/preferences` | 200 | Save current user’s dashboard preferences |
| GET | `/api/transactions` | 200 | Household transactions, newest first |
| GET | `/api/transactions/paged` | 200 | Filtered transaction page with total count |
| GET | `/api/transactions/category-breakdown` | 200 | Income or expense totals grouped by category |
| POST | `/api/transactions` | 201 | Create transaction and update snapshots |
| GET | `/api/transactions/{id}` | 200 | Editable transaction details, pair version, and optional bill link |
| PUT | `/api/transactions/{id}` | 200 | Correct a transaction and recalculate affected balances/trends |
| DELETE | `/api/transactions/{id}` | 204 | Delete a transaction or both linked transfer entries |
| POST | `/api/transaction-imports/validate` | 200 | Check included rows, categories, duplicates, and projected balance without writing |
| POST | `/api/transaction-imports` | 200 | Import a reviewed batch atomically or replay its result |
| GET | `/api/transaction-imports` | 200 | Last 50 household/account import batches |
| POST | `/api/transfers` | 201 | Create a transfer pair or its missing side linked to an existing entry atomically |
| POST | `/api/transfers/candidates` | 200 | Find existing unlinked entries before creating a transfer |
| POST | `/api/transfers/entries` | 201 | Record one unlinked or external transfer entry |
| POST | `/api/transfers/links` | 200 | Link two existing transfer entries without changing balances |
| DELETE | `/api/transfers/links/{transactionId}` | 204 | Unlink both sides, preserving entries and balances |
| GET | `/api/dashboard` | 200 | Totals, accounts with monthly trends, ten recent transactions |
| GET | `/api/dashboard/net-worth-trend` | 200 | Daily net-worth series for the requested range |
| GET | `/api/budgets/setup` | 200 | Budget setup status and resumable draft |
| GET | `/api/budgets/management` | 200 | Latest published item definitions and earliest allowed effective date |
| POST | `/api/budgets/items/preview` | 200 | Review one dated item change without publishing or writing data |
| POST | `/api/budgets/items` | 200 | Publish one reviewed item change, preserving past and paid occurrences |
| GET | `/api/budgets/draft` | 200 or 204 | Saved draft, or no content when absent |
| POST | `/api/budgets/draft` | 200 | Start or resume the household draft |
| PUT | `/api/budgets/draft/{draftId}` | 200 | Replace draft definitions and save wizard progress |
| GET | `/api/budgets/draft/{draftId}/preview` | 200 | Preview draft for a month and edit version |
| POST | `/api/budgets/draft/{draftId}/publish` | 200 | Publish the reviewed draft |
| GET | `/api/budgets/month` | 200 | Published monthly plan and calendar events |
| GET | `/api/recurring-expenses/settings` | 200 | Household daily-scan setting and last successful scan date |
| PUT | `/api/recurring-expenses/settings` | 200 | Save household daily-scan preference with version check |
| GET | `/api/csv-mappings` | 200 | Household saved CSV mapping profiles |
| POST | `/api/csv-mappings` | 200 | Create a named mapping profile |
| PUT | `/api/csv-mappings/{id}` | 200 | Update a profile with version checks |
| DELETE | `/api/csv-mappings/{id}?expectedEditVersion=1` | 204 | Delete profile metadata only |
| GET | `/api/merchant-rules` | 200 | Household rules, including disabled rules and archived-category status |
| POST | `/api/merchant-rules` | 200 | Create a merchant/category rule |
| PUT | `/api/merchant-rules/{id}` | 200 | Update/enable/disable a rule with version checks |
| DELETE | `/api/merchant-rules/{id}?expectedEditVersion=1` | 204 | Delete a rule without changing transactions |
| POST | `/api/merchant-rules/preview` | 200 | Preview recent recorded matches without saving |
| GET | `/api/alerts` | 200 | Active household alerts with current member's read status |
| GET | `/api/alerts/unread-count` | 200 | Current member's unread count |
| PUT | `/api/alerts/{id}/read` | 204 | Mark the specified alert version read for this member |
| POST | `/api/recurring-expenses/scan` | 200 | On-demand household recurring-expense suggestions |
| PUT | `/api/recurring-expenses/review` | 204 | Dismiss, match, or reset a recurring review |
| GET | `/api/budgets/forecast` | 200 | Running household income-period remainder through 1/3/6/12 months |
| GET | `/api/budgets/actuals/period` | 200 | Income-period planned/actual totals, daily graph, and paginated activity |
| GET | `/api/budgets/actuals` | 200 | Monthly allocation comparison and paginated activity assignments |
| PUT | `/api/budgets/actuals/categories/{categoryId}` | 204 | Save an expense category default effective from a month |
| PUT | `/api/budgets/actuals/transactions/{transactionId}` | 204 | Set/reset an individual movement's budget assignment |
| GET | `/api/budgets/income-period` | 200 | Current household or individual income period and scheduled bills |
| PUT | `/api/budgets/bills/{key}/{date}/payment` | 200 | Record or correct a scheduled bill/contribution payment and optional transaction link |
| GET | `/api/budgets/bills/{key}/{date}/transactions` | 200 | Suggested household expense entries for bills; expense/transfer entries for contributions |
| PUT | `/api/accounts/{id}` | 200 | Update account metadata/opening balance and rebuild history |
| PUT | `/api/categories/{id}` | 200 | Rename or archive/restore a household category |
| POST | `/api/transaction-imports/sessions/validate` | 200 | Review a multi-account session without writing |
| POST | `/api/transaction-imports/sessions` | 200 | Commit reviewed files and transfer links atomically |
| GET | `/api/transaction-imports/{batchId}/review` | 200 | Review remaining imported entries and original-state flags |
| POST | `/api/transaction-imports/undo/preview` | 200 | Preview selected removals, balances, links, and checkpoints |
| POST | `/api/transaction-imports/undo` | 200 | Apply acknowledged, fingerprint-validated undo atomically |
| GET | `/api/transfers/entries/{transactionId}/candidates` | 200 | Find an existing counterpart for an unlinked entry |
| POST | `/api/transfers/entries/{transactionId}/counterpart` | 201 | Create only a reviewed missing transfer side |
| POST | `/api/budgets/available-cash` | 200 | Read-only outlook from selected cash/card balances |
| GET | `/api/savings-funds` | 200 | Account makeup, funds, components and activity |
| POST | `/api/savings-funds/preview` | 200 | Review contribution schedule and affordability |
| POST | `/api/savings-funds` | 200 | Save reviewed fund and contribution plan |
| GET | `/api/savings-funds/{key}/candidates` | 200 | Find eligible contribution/spending transactions |
| POST | `/api/savings-funds/{key}/activity` | 200 | Record allocation, contribution, component move or use |
| POST | `/api/savings-funds/{key}/activity/{operation}/correct` | 200 | Correct recorded fund activity with version checks |
| POST | `/api/savings-funds/{key}/move` | 200 | Move allocated money between funds in one account |
| POST | `/api/savings-funds/{key}/archive` | 204 | Archive a fund without deleting history |
| GET | `/api/household-data/capabilities` | 200 | Hybrid availability, owner eligibility, upload limit |
| POST | `/api/household-data/backup` | 200 file | Password-authorized encrypted `.coiny` download |
| POST | `/api/household-data/export` | 200 file | Password-authorized financial CSV ZIP |
| POST | `/api/household-data/restore/preview` | 200 | Validate/decrypt multipart backup; issue short-lived preview |
| POST | `/api/household-data/restore/confirm` | 200 | Replace household finance after safety backup; return safety ID |
| GET | `/api/household-data/safety` | 200 | Latest ten household safety backups |
| POST | `/api/household-data/safety/{id}` | 200 file | Password-authorized installation-linked safety download |

Collection list responses remain JSON arrays. Paged transaction and category
breakdown endpoints return structured objects. Transaction details/update/delete, account updates, category management, and
session logout/revocation are exposed as listed above. Account deletion, refresh
tokens, and direct snapshot management are not exposed.

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
  "firstName": "Alex",
  "includeMortgageInNetWorth": true
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

Account responses also contain nullable `latestReconciliation`, with the checkpoint
shape below. Account listing returns the latest statement checkpoint and its current
review flag; newly created accounts return null.

## Account reconciliation

Apply `20261021123659_AddAccountReconciliations`. These routes require authentication
and current household membership; missing/foreign accounts return 404.

`POST /api/accounts/{{accountId}}/reconciliations/check`:

```json
{ "statementDate": "2026-10-16", "statementBalance": 2450.00 }
```

Both fields are required. The date must be on/after the opening date and no later
than the API server's current local date. Amounts permit signed values with at most
two decimals (18,2 range); positive liabilities represent amounts owed.

Returns `statementDate`, `statementBalance`, `calculatedBalance`, `difference`
(statement minus Coiny), `matches`, `ledgerFingerprint`, and `transactionCount`.
Calculation includes all transactions through that date, regardless of cleared
status, excludes later entries, and uses the account's normal balance rules.
The check writes nothing.

`POST /api/accounts/{{accountId}}/reconciliations`:

```json
{
  "statementDate": "2026-10-16",
  "statementBalance": 2450.00,
  "reviewedFingerprint": "<64-character fingerprint from the check response>",
  "transactionsReviewed": true
}
```

Confirmation requires explicit review, matching balances, and an unchanged
fingerprint, rechecked inside the save transaction. It returns the checkpoint:

```json
{
  "id": 1,
  "statementDate": "2026-10-16",
  "statementBalance": 2450.00,
  "confirmedAt": "2026-10-17T12:00:00Z",
  "confirmedByName": "Alex",
  "needsReview": false,
  "invalidatedAt": null,
  "invalidationReason": null
}
```

`GET /api/accounts/{{accountId}}/reconciliations` returns an array of the latest
50 checkpoints, newest statement date first, then confirmation time/ID. Repeating
a confirmation for an identical still-valid ledger returns the existing record.
Later edits/imports in the verified period or opening-balance/date corrections flag
it for review without removing history. A fresh verification saves a new record.
Transfer linking/unlinking alone preserves verification. No transactions are
created, cleared, reverted, or adjusted by reconciliation.

Errors: 400 missing/invalid values, unreviewed transactions, or mismatched balances;
401 unauthenticated; 403 no household; 404 missing/foreign account; 409 changed
ledger since the check. Error details use `ProblemDetails.detail`.
See [reconciliation rules](12-account-reconciliation.md).

## Budget actuals

Apply `20261021170126_AddBudgetActualAssignments` first. All routes require
authentication, household membership, and a published plan for the selected month.

`GET /api/budgets/actuals?month=2026-10-01&view=unassigned&type=Expense&page=1`
requires a first-of-month `month`. `view` is `unassigned` (default) or `all`;
optional `type` is `Expense` or `Transfer`. Pages contain 10 movements. Optional `allocationKey=<guid>` selects transactions
assigned to that item in the selected household/month, overriding the unassigned
view. Invalid or foreign item keys return 400. Type filtering still applies; omit
`type` to include expense and contribution-transfer entries together. Totals and
graph points remain for the full month. The response
contains `month`, nullable `throughDate`, `planned`, `assignedActual`,
`unassignedExpenses`, `awaitingTransaction`, `points`, `items`, `categoryDefaults`,
`transactions`, `page`, `pageSize`, and `totalCount`.

Items include stable `allocationKey`, `name`, `allocationType`, `planned`, `actual`,
`remaining`, `awaitingTransaction`, and `transactionCount`. Defaults include
`categoryId`, `categoryName`, `isActive`, nullable `allocationKey`, nullable
`effectiveFromMonth`, and `editVersion` (zero when no rule exists in this exact
month). Movements include `id`, `editVersion`, nullable `pairedEditVersion`, `date`,
`description`, nullable `merchant`, `amount`, `accountName`, `categoryName`, `categoryType`, nullable
`allocationKey`/`allocationName`, and `assignmentSource`
(`Payment`, `Override`, `Category`, or `Unassigned`).

`PUT /api/budgets/actuals/categories/1`:

```json
{ "month": "2026-10-01", "allocationKey": "<stable allocation GUID>", "expectedEditVersion": 0 }
```

The category must be an owned Expense category. The item must exist in that month's
published plan. Null allocationKey clears the default from this month forward;
older rules remain intact. Version is required and checked against the rule for
this exact effective month, not an inherited prior-month rule.

`PUT /api/budgets/actuals/transactions/1`:

```json
{
  "month": "2026-10-01",
  "allocationKey": "<stable allocation GUID>",
  "useCategoryDefault": false,
  "expectedEditVersion": 0,
  "expectedPairedEditVersion": null
}
```

Set null allocationKey with false to explicitly leave unassigned. Set
`useCategoryDefault: true` with null allocationKey to remove the override.
The boolean and transaction version are required; linked transfers require their
paired version too. Only Expense/Transfer entries are eligible; transfers can
target Savings/Investments only. Payment links take priority and must be detached
before adding overrides. Existing individual assignments must be reset before
attaching a scheduled payment link. Requests advance movement edit versions but
do not change categories, balances, dates, snapshots, or payment status.

Actuals use posting month, capped at today for the current month. Paired transfers
count once on the incoming entry's date. Bill links may identify an allocation
across months; their due date does not move recorded actuals. Unassigned transfers
are excluded from spending. Awaiting amounts remain planned values separate from
actuals. See [rules and UI](13-budget-actuals.md).

Errors: 400 invalid month/view/type/page, no plan, invalid/foreign category/item/
transaction, or ineligible target; 401 unauthenticated; 403 no household;
409 stale rule/movement or conflict with a payment assignment.

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

`POST /api/transfers` creates a linked transfer in one database transaction.
Without an existing entry, both new entries initially use the same date and cleared status:

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

Before saving, `POST /api/transfers/candidates` accepts the same From/To, amount,
and date fields and returns an array of transaction responses. It searches both
selected accounts for unlinked Transfer entries: Out in From, or In in To, with
the same amount and a posting date within ±7 days. Results prioritize nearest
dates, then date/ID. Both accounts must be active and in the current household.
This check creates nothing; equal amounts/dates are suggestions, not proof of a match.

To reuse one reviewed entry, include these fields in `POST /api/transfers`:

```json
{
  "fromAccountId": 1,
  "toAccountId": 2,
  "amount": 500.00,
  "date": "2026-10-17",
  "existingTransactionId": 102,
  "expectedExistingEditVersion": 0,
  "description": "New entry from Bills",
  "notes": null,
  "isCleared": true
}
```

The chosen entry must still be unlinked and match the selected accounts,
direction, amount, date window, and required edit version. Only the missing entry
is created. `date`, description, notes, and cleared status apply to that new entry;
the existing entry keeps its original posting date, details, cleared flag, and
import provenance. Only the new account's snapshots are recalculated. Entry
creation, pairing, and snapshots commit or roll back together. Retrying after
success returns 409 without adding another entry.

If candidates exist and neither is selected, creation returns 409 unless the user
explicitly chooses to create two new entries by supplying `createBothEntries: true`.
That flag cannot be combined with an existing-entry selection. With no matches,
omit it or send false so a newly appearing candidate at save time requires review.
Missing reviewed version is 400; stale/incompatible/already-linked selections are
409; a missing/foreign selected transfer is 404. If both sides already exist,
use `/transfers/links` to join them without creating anything.

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
  "secondTransactionId": 102,
  "expectedFirstEditVersion": 0,
  "expectedSecondEditVersion": 0
}
```

Both must be unlinked transfers in the current household, on different accounts,
with equal amounts and opposite directions. Posting dates may differ and are
preserved. An explicitly external entry can be linked when the user later tracks
the other account. Returns outgoing/incoming. No entries, balance effects, or
snapshots are added by linking. Optional expected edit versions reject stale review with 409; the UI always supplies both. No entries are linked automatically.

`DELETE /api/transfers/links/101?expectedEditVersion=1&expectedPairedEditVersion=1` unlinks both entries, sets each counterparty to
Unknown, and preserves all amounts, dates, cleared flags, and snapshots. This
does not undo or delete a transfer's financial effects. Optional expected versions protect against stale unlinking; the UI supplies both.

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
Link/unlink matching is available from transaction details. Multi-account import sessions use the endpoints below.

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

The inventory describes implemented contracts. Remaining release work and
post-v1 refinements are tracked in the
[root README](../README.md#release-status).


## Household budgeting

All routes require a bearer token and current household membership. Household IDs
are resolved from the authenticated user; clients do not supply them. Apply the
pending budget migrations with the [database setup command](06-development-setup.md#configure-and-start)
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


### Bill and contribution occurrence payment tracking

Use the allocation `sourceKey` from a published bill or contribution event and its ISO scheduled
`date`, for example `PUT /api/budgets/bills/{{billKey}}/2026-10-17/payment`.
Both routes require authentication and household membership. Only scheduled
Housing/Bills/Subscriptions/Savings/Investments events from the applicable published plan can be managed.
Monthly Savings/Investments also generate period-end set-asides. Income, Spending,
and payroll deductions are ineligible. Existing `/bills/` routes are reused;
apply the legacy payment/savings migrations (or hybrid schema chain) before use.

```json
{
  "isPaid": true,
  "paidOn": "2026-10-16",
  "note": "Paid online",
  "transactionId": null
}
```

`isPaid` is required. `paidOn` defaults to the linked transaction date, or today
in America/New_York for a manual payment; it cannot be a future date. Notes allow
500 characters. The optional `transactionId` must be a household Expense entry
for a bill; scheduled Savings/Investments can use Expense or Transfer entries.
Income is ineligible. An entry and its paired transfer entry cannot already pay
another occurrence. Either side of a transfer can be selected, but it counts once.
Send `isPaid: true` when linking.
To unlink while keeping paid status, retain `isPaid`/`paidOn` and send
`transactionId: null`. To mark unpaid, send `isPaid: false` and
`transactionId: null`; the stored payment date is cleared. PUT replaces the
occurrence's payment fields and returns those four fields, derived `status`, and nullable `linkedTransaction` (`id`, `date`, `amount`, `description`). No transaction
or balance is created or changed.

`status` is `Unpaid` when `isPaid` is false, `AwaitingTransaction` when paid with
no transaction link, or `Linked` when paid with a link. It is derived from the
existing fields, not independently stored or accepted as a state to write.
Unlinking a paid item (or deleting its linked entry) returns it to
`AwaitingTransaction`; its payment date/status remain independent.

`GET /api/budgets/bills/{{billKey}}/2026-10-17/transactions?search=electric`
returns up to 50 available entries with `id`, `date`, `amount`, `description`,
`accountName`, `categoryName`, `exactAmount`, `categoryType`, and nullable
`transferDirection`. Contribution suggestions include transfers; bill suggestions
remain expenses only. Without a search, candidates are
within 45 days either side of the due date; search covers all dates. Matching
amounts are prioritized, then nearby dates. Already-linked entries are excluded
unless linked to this occurrence; this exclusion also applies to the other side
of an assigned transfer. Linking two existing transfers returns 409 if both have
separate scheduled-payment assignments, requiring one assignment to be detached first.

Published month/income-period bill and contribution events include nullable `payment` with the
fields above. Income-period responses add `unpaidBillsTotal`; `billsTotal` remains
the complete scheduled total. `unpaidContributionsTotal` sums only unpaid scheduled
contributions; `contributionsTotal` remains their full planned total.
`awaitingTransactionCount` and `awaitingTransactionTotal` combine paid-unlinked
bills and contributions in that period; the amount is the sum of their planned
occurrence amounts. Those items are excluded from unpaid totals. Neither
payment action changes ledger entries or account snapshots. Draft allocation inputs/responses now carry optional
`sourceKey`, which must be retained on updates to preserve bill identity.

Errors: 400 invalid occurrence, payment date, or foreign/ineligible transaction;
401 unauthenticated; 403 no household; 409 transaction already linked or a write
conflict. Partial payments and multiple ledger entries per bill are not supported
in this first version.


### Editing and deleting transactions

`GET /api/transactions/{{transactionId}}` returns:

```json
{
  "transaction": {
    "id": 42,
    "editVersion": 0,
    "accountId": 1,
    "categoryId": 3,
    "categoryType": "Expense",
    "amount": 25.00,
    "transactionDate": "2026-10-16T00:00:00",
    "description": "Dinner",
    "merchant": "Cafe",
    "notes": null,
    "isCleared": true,
    "importBatchId": null,
    "pairedTransactionId": null
  },
  "pairedEditVersion": null,
  "bill": null
}
```

The nested `transaction` also includes the existing account, institution,
category, and transfer display fields. `bill`, when present, contains
`allocationKey`, `dueDate`, and `isPaid`. Transaction list projections now also
include `editVersion`, `notes`, and `importBatchId`.

Send the full editable state to `PUT /api/transactions/{{transactionId}}`:

```json
{
  "expectedEditVersion": 0,
  "expectedPairedEditVersion": null,
  "accountId": 1,
  "categoryType": "Expense",
  "categoryId": 3,
  "amount": 30.00,
  "date": "2026-10-16",
  "description": "Dinner",
  "merchant": "Cafe",
  "notes": "Corrected amount",
  "isCleared": true,
  "direction": null,
  "counterparty": null
}
```

The selected account/category must belong to the household. Choose an active
account/category, or retain the entry's existing inactive one. Amounts must be
positive and use at most two decimal places; dates cannot precede the selected
account's opening balance date. Description is required (250 characters), merchant
allows 150 characters, notes 1,000. Success returns refreshed edit details.

For a single Transfer entry, send `categoryType: "Transfer"`, `direction: "In"`
or `"Out"`, and `counterparty: "Unknown"` or `"External"`. The service resolves
its household Transfer category; `categoryId` is ignored. Unlinked entries can be
reclassified between Income, Expense, and Transfer. An expense linked to a bill
must stay Expense until explicitly unlinked from that bill.

For a linked transfer, retain its Transfer type/direction and supply the returned
`expectedPairedEditVersion`. Amount changes update both entries atomically;
account/date/description/merchant/notes/cleared changes apply only to the selected
entry. The two accounts must stay different. Existing source dates on the
counterpart are preserved to reflect different bank posting dates.

Delete using `DELETE /api/transactions/{{transactionId}}?expectedEditVersion=1`.
For a linked transfer, also include `expectedPairedEditVersion=1`; this deletes
**both entries**. No request body is needed. A stale/missing counterpart version
cannot authorize deleting a pair. Deletion is permanent. A bill reference is
cleared while its independent payment status/date/note remain. Import batch
history and its original imported count remain for provenance.

Every update/deletion recalculates snapshots from the earliest affected date in
each account, covering old/new dates, moved accounts, later history, and both
transfer accounts. Ledger changes, pair changes, and snapshots commit atomically.
`EditVersion` is a concurrency token; edits and transfer linking/unlinking advance
it. Reload details after 409 before retrying; do not silently retry stale writes.

Errors: 400 invalid fields/account/category/date/type; 401 unauthenticated;
403 no household; 404 missing/foreign transaction; 409 stale transaction/pair
or an incompatible bill link. DELETE requires `expectedEditVersion` in the query.


### Optional CSV bank balance check

The existing `POST /api/transaction-imports/validate` and
`POST /api/transaction-imports` requests accept optional `bankBalanceCheck`:

```json
{
  "date": "2026-10-16",
  "balance": 2450.00,
  "acknowledgedCalculatedBalance": null
}
```

`date` and `balance` are required when the object is supplied. The account must
be Checking or Savings; the date must be on/after its opening date and all included
row dates. Balance may be zero or negative, within 18,2 precision. Omitting the
object retains existing import behavior and preexisting submission hashes.

When rows are valid, validation adds `balanceComparison`:

```json
{
  "date": "2026-10-16",
  "bankBalance": 2450.00,
  "calculatedBalance": 2425.00,
  "difference": 25.00,
  "requiresAcknowledgment": true
}
```

The calculation uses opening balance and existing + included ledger entries
through the supplied date; future entries relative to that date are omitted.
`difference` is bank minus Coiny. Row errors prevent a comparison and leave
`balanceComparison` null. A mismatch sets `canImport: false` unless
`bankBalanceCheck.acknowledgedCalculatedBalance` exactly equals the calculated
balance. The client can present the acknowledgment after validation and submit
that reviewed value with the otherwise unchanged request.

Import revalidates under its existing database transaction. An unacknowledged or
changed difference returns 422 with fresh validation and writes nothing. A valid
acknowledgment allows importing without any adjustment. A changed bank reference
or acknowledgment changes the submission hash; reuse of an already-completed
submission ID with different data returns 409. Response/history shapes are
unchanged; this feature adds no persistent reconciliation records or migration.


### Loan subtypes and personal net-worth preferences

Account creation accepts optional `loanSubtype`: `Mortgage`, `Auto`, `Personal`,
`Student`, or `Other`, only when `accountType` is `Loan`. Omitted loan subtypes
default to Other. Non-loan accounts must omit it or send null. Account responses
and dashboard account summaries include nullable `loanSubtype`.

`PUT /api/accounts/{{accountId}}/loan-subtype` with `{ "loanSubtype": "Mortgage" }`
classifies an existing household Loan account. Success is 204; missing/foreign
accounts return 404, non-loans/invalid values return 400. This changes classification,
not account balances, ledger entries, or snapshots. Existing loans migrate to Other;
no mortgage inference is made from names.

`GET /api/users/me/preferences` returns `{ "includeMortgageInNetWorth": true }`.
`PUT` replaces that preference with the same shape; the boolean is required.
Both routes authenticate the current user, never accept a user/household ID, and
return 401 when unauthenticated or the authenticated user no longer exists.
The preference defaults to true for existing and new users. `/api/auth/me` also
includes `includeMortgageInNetWorth` for shared frontend state.

Dashboard `netWorth` adds `mortgage`, `hasMortgage`, and
`includeMortgageInNetWorth`. `debt` now means **Other Debt**, excluding mortgages.
Net-worth current value subtracts both Other Debt and Mortgage by default; when
the user excludes mortgages, only Other Debt is subtracted. Mortgage is summed
once from household Loan accounts explicitly classified Mortgage. `hasMortgage`
remains true for a zero-balance mortgage account.

`GET /api/dashboard/net-worth-trend` applies the same saved preference to every
daily point and derived dollar/percentage change. Account-specific trends and
recorded balances remain unchanged. Current loan classifications apply across
the displayed history. Each household member may choose their own view.

### Manage a household category

`PUT /api/categories/{id}` (Bearer authentication)

```json
{ "name": "Groceries", "isActive": true }
```

Returns the updated category. Names are trimmed, limited to 100 characters, and unique within the household (case insensitive, including archived names). Set `isActive` to `false` to archive or `true` to restore. Category type cannot be changed. System/transfer categories cannot be edited. Unknown or other-household IDs return 404; invalid names, duplicate names, missing active status, and protected categories return 400. Existing transactions retain their category ID and show the updated name. Archived categories remain in `GET /api/categories` for history and management, but cannot be selected for new transactions or CSV imports. Existing transactions can keep an archived category when edited.

### Edit an account

`PUT /api/accounts/{id}` (Bearer authentication) accepts the same fields as account creation and returns the updated account, including its recalculated current balance. Editable fields: `name`, `institutionId`, `loanSubtype` (loans only), `openingBalance`, and `openingBalanceDate`. Send the existing `accountType`; changing it is rejected to protect ledger interpretation. Opening balances must be nonnegative with at most two decimal places. The opening date cannot be in the future or after an existing transaction. The account and institution must belong to the current household. Missing/foreign accounts return 404; invalid edits return 400. Opening-balance/date changes rebuild stored balance history atomically and preserve transaction records. No database migration is required.

Dashboard summary `netWorth` includes `investments` (sum of Investment account balances) and `hasInvestments` (whether the household has any Investment accounts). These are separate from `retirement` and `liquidFunds`; net-worth calculation is unchanged.

### Reconcile separately recorded transfers

`GET /api/transfers/entries/{transactionId}/candidates?accountId=2&days=7`

Returns existing unlinked Transfer entries from the specified other account in the current household with equal amounts and opposite directions. The source must also be an unlinked Transfer. `days` defaults to 7 and accepts 0–365 (inclusive, before/after the source posting date). Results use the transaction response shape, including edit version and import batch, and sort by distance from the source date, then date and ID. Closed accounts can participate in existing-entry matching. It never creates or links entries. Foreign/missing source or target returns 404, same-account/invalid window returns 400, and an already-linked source returns 409. The client must let the user review and select; equal amounts and dates alone do not prove a transfer.

`POST /api/transfers/entries/{transactionId}/counterpart`

```json
{ "accountId": 2, "date": "2026-10-20", "expectedEditVersion": 0 }
```

Explicitly creates **one** missing counterpart, using the source amount and opposite direction, and links it to the existing source. Target account must be active, different, and in the current household; the date must be on/after its opening balance date. The source must still be unlinked and match the required expected edit version. The source posting date, amount, description, notes, cleared status, and import provenance remain intact. The new entry copies the description, starts uncleared, has no imported provenance, and uses the supplied posting date. Only the new entry’s account snapshots are recalculated. Changes commit atomically. Returns 201 with outgoing/incoming; stale/already-linked sources return 409. Retrying after success cannot add another counterpart. This operation does not infer whether another bank row exists; choose it only after checking existing entries. The Add Transaction flow can also select an existing entry using `POST /api/transfers` as described above.

### Multi-account CSV import sessions

`POST /api/transaction-imports/sessions/validate` and `POST /api/transaction-imports/sessions` require Bearer authentication and household membership.

```json
{
  "submissionId": "bcaecf20-488b-41e0-8914-f3d343a7a1ae",
  "files": [
    {
      "accountId": 1,
      "submissionId": "0550fab1-a431-491b-bfed-a14952a10484",
      "filename": "bills.csv",
      "rows": [{ "rowId": 2, "date": "2026-10-20", "amount": 250, "direction": "Out", "categoryType": "Transfer", "counterparty": "Unknown", "description": "Transfer to Spending", "isCleared": true, "allowDuplicate": false }]
    },
    {
      "accountId": 2,
      "submissionId": "2287c621-b557-4a5d-80e5-1da407b2feb1",
      "filename": "spending.csv",
      "rows": [{ "rowId": 2, "date": "2026-10-21", "amount": 250, "direction": "In", "categoryType": "Transfer", "counterparty": "Unknown", "description": "Transfer from Bills", "isCleared": true, "allowDuplicate": false }]
    }
  ],
  "links": [
    { "first": { "accountId": 1, "rowId": 2, "transactionId": null, "expectedEditVersion": null }, "second": { "accountId": 2, "rowId": 2, "transactionId": null, "expectedEditVersion": null } }
  ]
}
```

Each file uses the single-file request shape (including optional bank-balance acknowledgment). Limits: 1–8 files, one per account, distinct per-file submission IDs, 1–2,000 included rows per file, 8,000 included rows total. `links` may be empty. A link reference identifies either an included row (`accountId` + `rowId`) or an existing household transfer (`accountId` + `transactionId` + required `expectedEditVersion`). At least one side must be new. Each entry can participate only once; pairs require different accounts, equal amounts, and opposite directions.

Validation returns `{ canImport, files: [{ accountId, validation }], suggestions, moreSuggestions }`. Every per-file validation retains its usual categories, duplicates, projected balance, and bank-comparison information. Suggestions contain `first`/`second` entries with `reference`, `accountName`, `date`, `amount`, `direction`, and `description`. New rows are compared against each other and existing unlinked household transfers within ±7 days. Up to 200 suggestions are returned; ambiguous candidates remain choices rather than automatic matches. No writes occur during validation.

Import returns `{ submissionId, files: [singleFileImportResult], linkedPairs }`. All files and accepted pairings save atomically. The endpoint revalidates every file and accepted relationship inside the write transaction. Matching preserves existing entry balances/dates/provenance and adds no extra ledger movements. Every new included row is created once. Successful same-payload retries return the stored session result. Reusing that session ID with different files or links, or reusing a previously imported per-file submission, returns 409. Invalid row checks return 422 with a session validation response; stale matches return 409; malformed/invalid sessions return 400. Uncertain requests must be retried with the original immutable payload, not a new submission ID.

### Budget comparison daily series

The actuals response `points` contains one entry per calendar day: `date`,
`plannedSpending`, nullable `actualSpending`, `plannedContributions`, and nullable
`actualContributions`. Values are cumulative. Scheduled allocations enter on their
due dates; unscheduled monthly allowances accumulate evenly across the month.
Actual spending includes unassigned expenses and excludes contribution assignments.
Actual contributions include explicitly assigned or payment-linked contributions,
counting paired transfers once. Actual values are null after `throughDate` (all
null for future months). Activity filters and pagination do not filter this series.

Budget allocation types and setup steps now also accept `Subscriptions`. It is a
scheduled bill type for publication validation, income-period bill totals, and
payment linking, while appearing as a separate group in the budgeting UI.

## Income-period outlook and actuals

`GET /api/budgets/income-period?asOf=2026-10-30` now includes `plan` (the same
shape as a monthly plan, with amounts projected to the selected period),
`plannedPoints` (daily cumulative planned spending/contributions), and signed
`expectedRemaining`. A complete period starts on a payday and ends the day before
the next distinct household payday. Combined income, allocations, payroll details,
and generated set-asides are included in `plan`. An incomplete period has no plan.
Individual-source period lookup remains supported, but generated goals always use
the canonical household boundaries to avoid duplicate payment identities.

`GET /api/budgets/actuals/period?asOf=2026-10-30&view=all&page=1` accepts the same
activity filters and `allocationKey` as the monthly actuals endpoint. Its response
includes `startDate` and `endDate`; totals, graph points, and item transactions cover
that exact period. Transactions follow posting dates; paired transfers count once
on the receiving date. Category defaults follow each transaction's posting month.
Save transaction assignments with that transaction's first-of-month date. Category
rule edits remain monthly and use the period start month in the period editor.

Monthly allowances use differences of rounded cumulative monthly amounts, retaining
exact cents across period boundaries. Unscheduled Savings/Investments get generated
`isPeriodSetAside: true` events on the household period's final day; explicitly
scheduled contributions keep their dates. These events support the existing
bill-payment/candidate APIs without creating financial transactions automatically.
Publication preserves compatible generated-goal links and releases obsolete ones.
No new schema or reserve endpoint is required. Missing/invalid periods return 400;
authentication and household access checks continue to return 401/403.


## Payments across report periods

Budget actuals item responses additionally expose `priorPeriodActual` and
`futurePeriodActual`. These identify linked payments due outside the report's
start/end dates. Transaction responses expose nullable `paymentDueDate`. Actual,
raw remaining, and graph totals retain posting-date behavior; the UI's current
period remaining comparison adds these other-period amounts back to raw remaining.

## Published budget item updates

Published item editing uses `GET /api/budgets/management` to obtain `revision.id`,
the complete latest definitions (including schedules/source keys), and
`earliestEffectiveDate`. Submit the same body to `POST /api/budgets/items/preview`
and then `POST /api/budgets/items` after review:

```json
{
  "expectedRevisionId": 15,
  "effectiveDate": "2026-10-23",
  "isIncome": false,
  "sourceKey": "c89373f0-abb7-4727-8897-ef32e23f444d",
  "remove": false,
  "allocation": {
    "name": "Streaming",
    "amount": 15.99,
    "allocationType": "Subscriptions",
    "schedule": {
      "frequency": "Monthly",
      "startDate": "2026-10-01",
      "dayOfMonth": 25
    }
  }
}
```

For an addition, omit `sourceKey`. For removal, supply `sourceKey` and `remove:true`
and omit the definition. Income changes use `isIncome:true` and `income` instead
of `allocation`, with the existing paycheck schedule/deduction fields. Review
responses contain `before`, `after`, `beforePeriod`, `afterPeriod`, `effectiveDate`,
and `protectedPayments`. Validation returns 400, stale definitions or a saved
wizard draft return 409, unauthenticated requests return 401, and missing household
membership returns 403. Re-fetch management after every successful save. Changes
cannot predate today or the latest scheduled published update.


## Allocation forecast points

Both actuals endpoints include nullable `forecastSpending` and
`forecastContributions` on daily points. The recorded-through point is shared by
actual and forecast; future points retain null actuals and receive cumulative
remaining-plan estimates. Past points have null forecasts. Forecast is absent for
fully future or completed ranges. Activity filters and item detail pagination do
not change chart values. See [forecast rules](13-budget-actuals.md#forecast-continuation).


## Running forecast API

`GET /api/budgets/forecast?asOf=2026-10-23&months=3` uses the household income
period containing `asOf` (defaults to today). `months` defaults to 3 and accepts
only 0 (this period), 1, 3, 6, or 12. The response contains `startDate`, `endDate`,
nullable `throughDate`, `months`, `expectedIncomeAwaitingTransaction`, daily
`points`, and `periods`. Ranges always include whole periods and may end after
the exact calendar-month target. `expectedIncomeAwaitingTransaction` is retained
for response compatibility and is now zero: paydays today or earlier use actual
income only, while strictly future paydays use scheduled income.

Daily points contain `date`, signed cumulative `planned`, nullable `actual`, and
nullable `forecast`. All use a $0 view baseline. Period rows contain `startDate`,
`endDate`, `plannedIncome`, `plannedAllocations`, `plannedRemainder`,
`forecastIncome`, `forecastAllocations`, `forecastRemainder`, `cumulativePlanned`,
and `cumulativeForecast`. Historical rows use recorded results for their forecast
columns; current rows combine recorded and estimated activity; future rows use
remaining published commitments and income.

No complete published income period, unsupported horizon, invalid date, unsupported
calendar overflow, or an income schedule that cannot cover the horizon returns
400. Authentication and household membership follow the other budget endpoints.
The endpoint creates no transactions, payments, revisions, or financial snapshots.

Payment state responses include nullable `actualPostingDate`. For paired contribution
transfers this is the receiving entry’s posting date, matching actuals counting;
`linkedTransaction` continues to identify the selected linked entry.


Running forecast responses also contain `plannedSavingsContributions`,
`forecastSavingsContributions`, `plannedInvestmentContributions`,
`forecastInvestmentContributions`, `additionalSavings`, `fundingShortfall`,
`potentialSavings`, and `shortfallBeyondSavings`. These are range totals, not
account balances. Potential savings combines forecast savings contributions and
signed ending remainder, floored at zero. Investments are excluded. Actuals
responses expose `forecastSavingsContributions` and
`forecastInvestmentContributions` as the recorded-plus-remaining contribution
split used in that calculation. No storage changes or financial writes are made.

## Recurring expense detection

`POST {{baseUrl}}/recurring-expenses/scan` with `{}` scans the last 30 months of
recorded positive household expenses through today (America/New_York). The scan
is read-only. Income, transfers, refunds, and future transactions are excluded.
The response contains `fromDate`, `throughDate`, `transactionsScanned`,
`suggestions`, and `budgetItems` (`key`, `name`). Each suggestion has `key`,
`merchant`, `frequency`, `typicalAmount`, `minimumAmount`, `maximumAmount`,
`lastSeen`, `nextExpected`, `transactionCount`, `status` (`NeedsReview`, `Covered`,
`Dismissed`), nullable `allocationKey` and `suggestedAllocationKey`, `reviewed`, and
up to 12 `transactions` (`id`, `date`, `amount`, `accountName`, `description`).
A suggested budget name requires confirmation. A confirmed match or a supporting
payment link to a current scheduled item makes a pattern Covered.

`PUT {{baseUrl}}/recurring-expenses/review` accepts:

```json
{ "key": "<64-character pattern key from the scan>", "action": "Match", "allocationKey": "<current budget source key>" }
```

Actions are `Match`, `Dismiss`, and `Reset`. Only Match requires `allocationKey`;
other actions use null. The server rescans to validate that the pattern still
exists and the target is a current scheduled housing/bill/subscription item.
Reviews are household-scoped and do not create transaction assignments, payment
links, or budget items. Reset removes a saved review, while independent existing
payment evidence can still mark a pattern Covered. Invalid patterns/actions/targets
return 400; conflicting saves return 409; missing authentication returns 401;
missing household access returns 403. Scan returns 200; review returns 204.

See [detection and review rules](14-recurring-detection.md). Automatic scanning and
alerts are opt-in; see the settings and alerts endpoints below.

## Daily scan settings and household alerts

`GET {{baseUrl}}/recurring-expenses/settings` returns
`{ "enabled": false, "lastScanDate": null, "editVersion": 0 }` when unset.
`PUT` to the same path requires both `enabled` and `expectedEditVersion`:

```json
{ "enabled": true, "expectedEditVersion": 0 }
```

It returns the saved setting with the incremented edit version. This is a shared
household preference accessible to household members; it defaults off. Automatic
scans also advance its edit version. A stale version returns 409. Missing/invalid
fields return 400. Settings access requires authentication (401) and household
membership (403).

`GET {{baseUrl}}/alerts?page=1` returns `unreadCount`, `total`, `page`, and `items`.
Pages contain 20 active alerts ordered by update date/id descending. Items contain
`id`, `merchant`, `frequency`, `typicalAmount`, `nextExpected`, `updatedAt` (UTC),
`unread`, and `version`. `GET {{baseUrl}}/alerts/unread-count` returns
`{ "unreadCount": 3 }`. These reads do not mark alerts read.

`PUT {{baseUrl}}/alerts/4/read` with `{ "version": 1 }` marks that version read
for the authenticated member and returns 204. A valid older version does not clear
a newer one. Unknown/foreign/resolved ids and invalid versions/pages return 400;
conflicting receipt writes return 409. Missing fields return 400; missing auth or
household access returns 401/403. The service never accepts a user id or household
id from the client.

Only automatic scans create alerts; manual pattern reviews can resolve them.
The worker checks every 15 minutes and records one successful scan per enabled
household per New York calendar day. It needs a running API and the additive alert
migration; no SMTP/text provider is used. See [automation rules](14-recurring-detection.md#optional-automatic-scans-and-alerts).

The Settings page’s Preferences section submits its personal and household toggles together through
`PUT /api/users/me/preferences`. In addition to required `includeMortgageInNetWorth`,
it optionally accepts `recurringDetection: { enabled, expectedEditVersion }` with
the same validation/version rules as the household settings endpoint. Omitting it
leaves household detection unchanged. Legacy mode commits both in one database
transaction. Hybrid mode records a durable central intent and local receipt so
interrupted local commits can complete idempotently; it is not a cross-database
transaction. Conflicting versions require review rather than silently overwriting.
The response remains the personal preference object. Settings
are staged until Save settings; changing either toggle alone makes no write.

## Merchant/category rules

All `/api/merchant-rules` routes require authentication and current household
membership (401/403). Clients never supply household ids. GET lists household
rules, including disabled rules and ones targeting archived categories. Responses
contain `id`, `matchType`, `pattern`, `categoryId`, `categoryName`, `categoryType`,
`categoryActive`, `isActive`, and `editVersion`.

POST creates, PUT `/api/merchant-rules/{id}` updates, and POST
`/api/merchant-rules/preview` previews this required payload:

```json
{
  "matchType": "MerchantExact",
  "pattern": "NETFLIX.COM",
  "categoryId": 7,
  "isActive": true,
  "expectedEditVersion": 0
}
```

Match types are `MerchantExact` and `DescriptionContains`. Phrases are 3–200
characters after normalization; whitespace/punctuation-only phrases are invalid.
Targets must be active Income/Expense categories in the current household.
An existing rule can be disabled while retaining its archived target. Normalized
match type/phrase pairs are unique per household, including disabled rules.
Create uses version 0; updates use the returned edit version and increment it.
Duplicate phrases and stale updates/deletes return 409. Invalid ids/targets/payloads
return 400. DELETE requires `expectedEditVersion` in the query string.

Preview returns `transactionsChecked` and `examples`, up to 10 matching entries
from the latest 1,000 household transactions of the selected category type. Examples
contain `id`, `date`, `merchant`, `description`, `amount`, and `accountName`.
Preview never saves a rule or changes transactions. It checks recorded history,
not the CSV file currently in the browser.

Import review fetches this rule list and matches locally: exact merchant wins;
otherwise the longest normalized description phrase wins. Conflicting equal-rank
categories require user selection. Known transfer/excluded rows, archived targets,
disabled rules, and opposite money directions are skipped. Suggestions feed the
normal import validation; the server still validates category ownership/activity
and all row/duplicate/balance constraints before import. No rule is run against
existing ledger entries. See [rule behavior](15-merchant-category-rules.md).

## Saved CSV mappings

`GET {{baseUrl}}/csv-mappings` lists all profiles in the current household. POST
creates and PUT `/csv-mappings/{id}` updates. Both return `id`, `name`, `accountType`,
nullable `institutionId`, `settings`, and `editVersion`. Sample create payload:

```json
{
  "name": "Community Bank checking",
  "accountType": "Checking",
  "institutionId": null,
  "expectedEditVersion": 0,
  "settings": {
    "headers": ["Date", "Description", "Amount"],
    "encoding": "utf-8",
    "delimiter": ",",
    "headerLine": 1,
    "balanceOrder": null,
    "mapping": {
      "dateColumn": 0,
      "descriptionColumn": 1,
      "dateFormat": "MM/DD/YY",
      "decimalSeparator": ".",
      "excludedStatuses": [],
      "amount": { "mode": "signed", "column": 2, "positiveMeans": "In" }
    }
  }
}
```

Column indexes are zero-based in the saved header list. Amount modes are `signed`
(`column`, `positiveMeans` In/Out), `split` (`debitColumn`, `creditColumn`), and
`indicator` (`column`, `directionColumn`, `directions` mapping bank codes to In/Out).
Optional mapping indexes: `statusColumn`, `classificationColumn`, `balanceColumn`,
`merchantColumn`, `accountColumn`. Optional `balanceOrder` is NewestFirst/OldestFirst.
Profiles store no source-account selection, transaction rows, filename, import
confirmation, or balance acknowledgment. Unknown payload fields are not persisted.

Validation requires a nonblank name of up to 100 characters, a valid account type,
a household-owned institution if specified, 2–100 header labels of at most 200
characters, valid distinct mapped indexes, nonblank mapped labels, supported date
formats and amount direction, decimal separator dot/comma, supported encoding
(utf-8/windows-1252/utf-16le), separator comma/semicolon/tab, header line 1–100, and
up to 50 excluded status values and indicator codes (100 characters each).
Normalized names are unique per household. The service caps households at 100
profiles. Updates/deletes require the current version; create uses 0. Conflicting
names or stale saves/deletes return 409. Invalid/foreign ids or institutions and
invalid payloads return 400; missing authentication/household returns 401/403.
DELETE requires `expectedEditVersion` in the query and returns 204.

The browser filters profiles by account type and institution and rebinds columns
by header name before use. Neither saved mappings nor their endpoints import or
modify transactions. Existing import validation remains authoritative. Apply
`20261025190224_AddSavedCsvMappings` and restart the API before using these routes.
See [saved mapping behavior](16-saved-csv-mappings.md).

### Multiple transactions for a scheduled payment

Bill/contribution payment PUT accepts optional `transactionIds` (up to 50 unique IDs), replacing the complete linked set. An empty array detaches all links while retaining manual paid status. Legacy `transactionId` requests remain supported; if both fields are supplied, the single ID must belong to the array. Responses include `transactionIds` and `linkedTransactions`, each with its canonical `actualPostingDate`; legacy single fields describe the first surviving link. Each movement may belong to only one scheduled occurrence or individual assignment. Select only one side of a paired transfer.

Actuals sum all linked transactions on their posting dates, including charges split across months. Forecasts include known future linked amounts once and do not add the settled occurrence's planned amount again. Removing an item releases every associated link. Deleting a transaction removes its link while retaining other links and the independent paid status. Links never create ledger entries or modify balances.

### Available cash outlook

`POST /api/budgets/available-cash` calculates a read-only account-based outlook. Accepts optional `asOf` and nullable `accountIds`; returns current cash/card balances, remaining expected income/allocations, the ending estimate, and eligible account options. Authentication and household ownership are required. See [Available cash outlook](17-available-cash-outlook.md) for defaults, account eligibility, range limits, and response fields.

### Import undo and correction

`GET /api/transaction-imports/{batchId}/review`, `POST /api/transaction-imports/undo/preview`, and `POST /api/transaction-imports/undo` provide household-scoped review and an atomic, fingerprint-validated undo. See [Import undo/correction](18-import-undo-correction.md) for request/response behavior, explicit acknowledgments, transfer preservation, reconciliation invalidation, and retry semantics.

Savings fund endpoints and activity semantics: [Savings funds](19-savings-funds.md#api).
Set `plan.scheduleContributions: false` for an unscheduled, existing-cash fund.
Its preview has no periods and saving it does not create or change a budget item.
Existing plans default to scheduled contributions when the property is omitted.

See [account security](20-account-security.md) for password request bodies, session expiry/revocation, rate limits, and SMTP requirements.

## Household encrypted backup, restore, and export

These authenticated routes are enabled only for hybrid storage. `capabilities`
returns `enabled`, `owner`, and `maximumFileBytes`; legacy mode returns disabled.
Other data operations require household ownership. Backup, export, preview,
confirmation, and safety download additionally verify `currentPassword`.

- `backup` JSON: `currentPassword`, `passwordProtected` (boolean), optional
  `backupPassword`. Returns an encrypted binary `.coiny` file. The optional backup
  password is separate from the account password and makes it portable.
- `export` JSON: `currentPassword`. Returns an unencrypted financial CSV ZIP;
  it is not a restorable backup and excludes central identities.
- `restore/preview` multipart form: `file`, `currentPassword`, optional
  `backupPassword`. Returns `previewId`, `expiresAt`, `backupCreatedAt`, `portable`,
  `backupAccounts`, `backupTransactions`, `currentAccounts`, `currentTransactions`.
- `restore/confirm` JSON: `previewId`, `currentPassword`, `confirmReplacement: true`.
  Returns `safetyBackupId` after safety capture and atomic registry replacement.
- `safety/{id}` POST JSON: `currentPassword`. Returns the encrypted safety file.

Preview expires in 15 minutes and becomes stale when target data changes.
Invalid inputs return 400, owner failures 403, stale/conflicting state 409, and
busy/rate-limited operations 429; missing authentication returns 401. Responses
use `Cache-Control: no-store`. Uploads are limited to 64 MiB; SQLite payloads to
32 MiB. See [household data tools](23-household-backup-restore-export.md) for
validation, key requirements, retained files, and cross-installation boundaries.

## Skipping a savings contribution

The occurrence payment PUT also accepts `isSkipped` with an optional note for
savings contributions. Stored period boundaries suppress the contribution from
that period's planned/forecast commitments without creating actual activity or
moving account cash. The state can be undone; transaction-linked occurrences must be explicitly
unlinked first. See [skip semantics](19-savings-funds.md#skipping-a-savings-contribution).
